/* openvr_api.dll of XIII VR: the runtime switch.
 *
 * 0.1.180: the game's Unity VR module
 * (XRSDKOpenVR.dll) speaks OpenVR only. At the first call this DLL looks at
 * the Windows default OpenXR runtime (XR_RUNTIME_JSON, else
 * HKLM\SOFTWARE\Khronos\OpenXR\1 ActiveRuntime) and loads, from its own folder:
 *   SteamVR (or no OpenXR runtime)  -> openvr_api_valve.dll (Valve's own, SteamVR directly)
 *   any other (Pimax Play, Oculus/Meta, Virtual Desktop, WMR, ...)
 *                                   -> openvr_api_oc.dll (OpenComposite: OpenVR on OpenXR)
 * The mod's setting [VR] Runtime (Auto/SteamVR/OpenXR) reaches it as the
 * process variable XIIIVR_RUNTIME. A missing DLL falls back to the other.
 * Every export calls the chosen DLL's own (all of them take at most four
 * integer/pointer arguments and return an integer or pointer, so one
 * pass-through shape fits them all).
 *
 * With OpenComposite (0.1.177-0.1.178): the Unity module imports functions
 * OpenComposite lacks (VRHeadsetView, ...: answered NULL, the module checks)
 * and asks for interface versions newer than OpenComposite implements
 * (IVRSystem_023, IVRCompositor_029, IVROverlay_028; OpenComposite stops the
 * game on an unknown version). When OpenComposite lacks the version asked for
 * but has the previous one, an adapter is returned: the newer versions only
 * ADDED methods (checked against the headers, see maps.json), so each new slot
 * calls the same method in the old version; the added methods answer "not
 * available". The mod's own FnTable interfaces are adapted the same way.
 *
 * Built with clang + lld-link (build.sh), no C runtime, no Windows SDK.
 */
#include "adapters.inc"

typedef unsigned long long u64;
typedef unsigned int u32;
typedef unsigned short u16;
typedef unsigned char u8;

__declspec(dllimport) void *LoadLibraryExA(const char *name, void *file, u32 flags);
__declspec(dllimport) void *GetProcAddress(void *module, const char *name);
__declspec(dllimport) u32 GetModuleFileNameA(void *module, char *name, u32 size);
__declspec(dllimport) u32 GetEnvironmentVariableA(const char *name, char *value, u32 size);
__declspec(dllimport) u32 GetFileAttributesA(const char *name);
__declspec(dllimport) long RegGetValueA(void *key, const char *subkey, const char *value, u32 flags, u32 *type, void *data, u32 *size);

int _fltused = 0; /* the MSVC target wants it once floats appear; no C runtime here */
extern char shim_thunk_base[]; /* thunks.S: thunk k at +16*k calls inner vtable slot k */
extern char __ImageBase[];     /* this DLL (the linker's symbol) */

typedef void *(*GetInterfaceFn)(const char *name, int *error);
typedef u64 (*Pass)(u64, u64, u64, u64);

/* ---- small helpers (no C runtime) ---- */
static int slen(const char *s) { int n = 0; while (s[n]) n++; return n; }
static int same(const char *a, const char *b) { while (*a && *a == *b) { a++; b++; } return *a == *b; }
static int prefix(const char *s, const char *p) { while (*p) { if (*s != *p) return 0; s++; p++; } return 1; }
static char lower(char c) { return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c; }
static int same_nocase(const char *a, const char *b) { while (*a && lower(*a) == lower(*b)) { a++; b++; } return lower(*a) == lower(*b); }
static int contains_nocase(const char *s, const char *p)
{
    for (; *s; s++) { int k = 0; while (p[k] && lower(s[k]) == lower(p[k])) k++; if (!p[k]) return 1; }
    return 0;
}
static int append(char *out, int size, int n, const char *s) { while (s && *s && n < size - 1) out[n++] = *s++; out[n] = 0; return n; }

/* Does the loaded image contain the NUL-terminated string s (OpenComposite
 * keeps every interface version it implements as such a string)? */
static int image_has(const u8 *b, const char *s)
{
    if (!b || b[0] != 'M' || b[1] != 'Z') return 0;
    const u8 *nt = b + *(const u32 *)(b + 0x3C);
    if (*(const u32 *)nt != 0x4550) return 0;
    u16 sections = *(const u16 *)(nt + 6), optional = *(const u16 *)(nt + 20);
    u32 image = *(const u32 *)(nt + 24 + 56);
    const u8 *sec = nt + 24 + optional;
    int n = slen(s) + 1; /* with the terminating NUL */
    for (u32 i = 0; i < sections; i++) {
        const u8 *h = sec + 40 * i;
        u32 size = *(const u32 *)(h + 8), va = *(const u32 *)(h + 12), raw = *(const u32 *)(h + 16), flags = *(const u32 *)(h + 36);
        if (!(flags & 0x40000000u)) continue; /* not readable */
        if (!size) size = raw;
        if (va >= image) continue;
        if (size > image - va) size = image - va;
        if (size < (u32)n) continue;
        const u8 *p = b + va, *end = p + size - n;
        for (; p <= end; p++) {
            if (*p != (u8)s[0]) continue;
            int k = 1;
            while (k < n && p[k] == (u8)s[k]) k++;
            if (k == n) return 1;
        }
    }
    return 0;
}

/* ---- report (read by the mod: XIIIVR_ShimReport) ---- */
static char report[1536] = "XIII VR runtime switch 0.1.180:";
static void note(const char *a, const char *b, const char *c)
{
    int n = slen(report);
    n = append(report, sizeof(report), n, a);
    n = append(report, sizeof(report), n, b);
    append(report, sizeof(report), n, c);
}

/* ---- choosing the runtime ---- */
enum { NONE = 0, VALVE = 1, OC = 2 };
/* mode: auto / steamvr / openxr; runtime: the default OpenXR runtime's manifest ("" none). */
static int decide(const char *mode, const char *runtime, int has_valve, int has_oc)
{
    int want_oc;
    if (same_nocase(mode, "steamvr")) want_oc = 0;
    else if (same_nocase(mode, "openxr")) want_oc = 1;
    else want_oc = runtime[0] && !contains_nocase(runtime, "steamxr") && !contains_nocase(runtime, "\\steamvr\\") && !contains_nocase(runtime, "/steamvr/");
    if (want_oc && has_oc) return OC;
    if (has_valve) return VALVE;
    return has_oc ? OC : NONE;
}
/* For the test only (shimtest.c): the same decision. */
__declspec(dllexport) int XIIIVR_Decide(const char *mode, const char *runtime, int has_valve, int has_oc) { return decide(mode, runtime, has_valve, has_oc); }

enum {
    E_GETGENERICINTERFACE, E_GETINITTOKEN, E_GETRUNTIMEPATH, E_GETSTRINGFORHMDERROR, E_ERRORENGLISH, E_ERRORSYMBOL,
    E_INITINTERNAL, E_INITINTERNAL2, E_ISHMDPRESENT, E_ISINTERFACEVERSIONVALID, E_ISRUNTIMEINSTALLED, E_RUNTIMEPATH,
    E_SHUTDOWNINTERNAL, E_HEADSETVIEW, E_PATHS, E_CONTROLPANEL, E_VIRTUALDISPLAY, E_LIQUIDVR, E_COUNT
};
static const char *const export_names[E_COUNT] = {
    "VR_GetGenericInterface", "VR_GetInitToken", "VR_GetRuntimePath", "VR_GetStringForHmdError",
    "VR_GetVRInitErrorAsEnglishDescription", "VR_GetVRInitErrorAsSymbol", "VR_InitInternal", "VR_InitInternal2",
    "VR_IsHmdPresent", "VR_IsInterfaceVersionValid", "VR_IsRuntimeInstalled", "VR_RuntimePath",
    "VR_ShutdownInternal", "VRHeadsetView", "VRPaths", "VRControlPanel", "VRVirtualDisplay", "LiquidVR"
};
static void *fn[E_COUNT];
static volatile int chosen;
static int backend_kind;
static void *oc_module;
static GetInterfaceFn oc_get;

static void choose(void)
{
    if (chosen) return;
    char valve[600], oc[600], mode[32], runtime[600];
    u32 n = GetModuleFileNameA(__ImageBase, valve, 520);
    if (n == 0 || n >= 520) n = 0;
    while (n > 0 && valve[n - 1] != '\\' && valve[n - 1] != '/') n--;
    valve[n] = 0;
    for (u32 i = 0; i <= n; i++) oc[i] = valve[i];
    append(valve, sizeof(valve), (int)n, "openvr_api_valve.dll");
    append(oc, sizeof(oc), (int)n, "openvr_api_oc.dll");
    int has_valve = GetFileAttributesA(valve) != 0xFFFFFFFFu, has_oc = GetFileAttributesA(oc) != 0xFFFFFFFFu;
    u32 m = GetEnvironmentVariableA("XIIIVR_RUNTIME", mode, sizeof(mode));
    if (m == 0 || m >= sizeof(mode)) append(mode, sizeof(mode), 0, "auto");
    const char *source = "XR_RUNTIME_JSON";
    u32 r = GetEnvironmentVariableA("XR_RUNTIME_JSON", runtime, sizeof(runtime));
    if (r == 0 || r >= sizeof(runtime)) {
        u32 size = sizeof(runtime);
        source = "Windows default";
        if (RegGetValueA((void *)(long long)(int)0x80000002, "SOFTWARE\\Khronos\\OpenXR\\1", "ActiveRuntime", 0x2 /* RRF_RT_REG_SZ */, 0, runtime, &size) != 0) runtime[0] = 0;
    }
    int kind = decide(mode, runtime, has_valve, has_oc);
    void *module = kind ? LoadLibraryExA(kind == OC ? oc : valve, 0, 0x8 /* LOAD_WITH_ALTERED_SEARCH_PATH */) : 0;
    if (!module && kind) {
        /* the chosen one did not load: the other, if there */
        int other = kind == OC ? VALVE : OC;
        if (other == OC ? has_oc : has_valve) { module = LoadLibraryExA(other == OC ? oc : valve, 0, 0x8); if (module) { note(" (", kind == OC ? "openvr_api_oc.dll" : "openvr_api_valve.dll", " did not load)"); kind = other; } }
    }
    if (!module) kind = NONE;
    for (int i = 0; i < E_COUNT; i++) fn[i] = module ? GetProcAddress(module, export_names[i]) : 0;
    backend_kind = kind;
    if (kind == OC) { oc_module = module; oc_get = (GetInterfaceFn)fn[E_GETGENERICINTERFACE]; if (!oc_get) backend_kind = NONE; }
    note(" OpenXR runtime (", source, "): ");
    note(runtime[0] ? runtime : "none", "; setting ", mode);
    note("; ", backend_kind == OC ? "-> OpenComposite (openvr_api_oc.dll)" : backend_kind == VALVE ? "-> SteamVR (openvr_api_valve.dll)" : "-> no OpenVR runtime DLL found", has_oc ? "" : "; OpenComposite not installed");
    note(";", 0, 0);
    chosen = 1;
}

__declspec(dllexport) const char *XIIIVR_ShimReport(void) { choose(); return report; }

/* Every export: the chosen DLL's own; what it lacks answers "nothing". */
static const char no_runtime[] = "VRInitError_Init_InstallationNotFound (XIII VR: no openvr_api_valve.dll / openvr_api_oc.dll)";
static u64 pass(int i, u64 a, u64 b, u64 c, u64 d, u64 fallback)
{
    choose();
    return fn[i] ? ((Pass)fn[i])(a, b, c, d) : fallback;
}
#define PASS(name, index, fallback) \
    __declspec(dllexport) u64 name(u64 a, u64 b, u64 c, u64 d) { return pass(index, a, b, c, d, fallback); }
PASS(VR_GetInitToken, E_GETINITTOKEN, 0)
PASS(VR_GetRuntimePath, E_GETRUNTIMEPATH, 0)
PASS(VR_GetStringForHmdError, E_GETSTRINGFORHMDERROR, (u64)no_runtime)
PASS(VR_GetVRInitErrorAsEnglishDescription, E_ERRORENGLISH, (u64)no_runtime)
PASS(VR_GetVRInitErrorAsSymbol, E_ERRORSYMBOL, (u64)no_runtime)
PASS(VR_IsHmdPresent, E_ISHMDPRESENT, 0)
PASS(VR_IsInterfaceVersionValid, E_ISINTERFACEVERSIONVALID, 0)
PASS(VR_IsRuntimeInstalled, E_ISRUNTIMEINSTALLED, 0)
PASS(VR_RuntimePath, E_RUNTIMEPATH, (u64)"")
PASS(VR_ShutdownInternal, E_SHUTDOWNINTERNAL, 0)
/* Accessors XRSDKOpenVR.dll imports; OpenComposite lacks them (NULL: the module checks). */
PASS(VRHeadsetView, E_HEADSETVIEW, 0)
PASS(VRPaths, E_PATHS, 0)
PASS(VRControlPanel, E_CONTROLPANEL, 0)
PASS(VRVirtualDisplay, E_VIRTUALDISPLAY, 0)
PASS(LiquidVR, E_LIQUIDVR, 0)
/* VR_InitInternal(EVRInitError *error, type[, startup]): with no runtime DLL, error 100 (installation not found). */
static u64 init(int i, u64 a, u64 b, u64 c, u64 d)
{
    choose();
    if (fn[i]) return ((Pass)fn[i])(a, b, c, d);
    if (a) *(int *)a = 100;
    return 0;
}
__declspec(dllexport) u64 VR_InitInternal(u64 a, u64 b, u64 c, u64 d) { return init(E_INITINTERNAL, a, b, c, d); }
__declspec(dllexport) u64 VR_InitInternal2(u64 a, u64 b, u64 c, u64 d) { return init(E_INITINTERNAL2, a, b, c, d); }

/* 0 = not looked yet, 1 = OpenComposite has it, 2 = it does not */
static int oc_has(const char *version, int *cache)
{
    if (!*cache) *cache = image_has((const u8 *)oc_module, version) ? 1 : 2;
    return *cache == 1;
}

/* ---- adapters ---- */
typedef struct { void **vtbl; void *inner; } Adapter;
#define COUNT(a) ((int)(sizeof(a) / sizeof((a)[0])))
_Static_assert(THUNKS <= 96, "adapter tables hold 96 slots");

enum { SYSTEM, COMPOSITOR, OVERLAY };
typedef struct {
    int family;
    const char *old, *oldTable;
    const short *map;
    int count, oldCount;
    int has, noted, notedTable, built;
    void *vtbl[96];
    void *table[96];
    Adapter object;
    void **inner_table;
} Candidate;

static Candidate system_022 = { SYSTEM, "IVRSystem_022", "FnTable:IVRSystem_022", MAP_SYSTEM_022, COUNT(MAP_SYSTEM_022), OLD_COUNT_SYSTEM_022 };
static Candidate compositor_028 = { COMPOSITOR, "IVRCompositor_028", "FnTable:IVRCompositor_028", MAP_COMPOSITOR_028, COUNT(MAP_COMPOSITOR_028), OLD_COUNT_COMPOSITOR_028 };
static Candidate compositor_027 = { COMPOSITOR, "IVRCompositor_027", "FnTable:IVRCompositor_027", MAP_COMPOSITOR_027, COUNT(MAP_COMPOSITOR_027), OLD_COUNT_COMPOSITOR_027 };
static Candidate overlay_027 = { OVERLAY, "IVROverlay_027", "FnTable:IVROverlay_027", MAP_OVERLAY_027, COUNT(MAP_OVERLAY_027), OLD_COUNT_OVERLAY_027 };

typedef struct { const char *want; int has; Candidate *candidates[3]; } Family;
static Family families[] = {
    { "IVRSystem_023", 0, { &system_022, 0, 0 } },
    { "IVRCompositor_029", 0, { &compositor_028, &compositor_027, 0 } },
    { "IVROverlay_028", 0, { &overlay_027, 0, 0 } },
};

/* The methods the old versions lack. C++ ones get the adapter as `this`. */
typedef _Bool (*PollWithPose)(void *self, int origin, void *event, u32 size, void *pose);
typedef _Bool (*PollWithPoseTable)(int origin, void *event, u32 size, void *pose);
static _Bool poll_with_overlays(Adapter *self, int origin, void *event, u32 size, void *pose, u64 *overlay)
{
    if (overlay) *overlay = 0;
    return ((PollWithPose)((void **)*(void **)self->inner)[OLD_SYSTEM_022_POLLNEXTEVENTWITHPOSE])(self->inner, origin, event, size, pose);
}
static _Bool poll_with_overlays_table(int origin, void *event, u32 size, void *pose, u64 *overlay)
{
    if (overlay) *overlay = 0;
    return ((PollWithPoseTable)system_022.inner_table[OLD_SYSTEM_022_POLLNEXTEVENTWITHPOSE])(origin, event, size, pose);
}
static int compositor_failed(void) { return 1; }                  /* VRCompositorError_RequestFailed */
static int overlay_failed(void) { return 23; }                    /* VROverlayError_RequestFailed */
static int create_subview(Adapter *self, u64 parent, const char *key, const char *name, u64 *handle)
{ (void)self; (void)parent; (void)key; (void)name; if (handle) *handle = 0; return 23; }
static int create_subview_table(u64 parent, const char *key, const char *name, u64 *handle)
{ (void)parent; (void)key; (void)name; if (handle) *handle = 0; return 23; }

/* OpenComposite stops the game in these (its "Hit stubbed file"); here they answer nothing. */
static u64 answer_zero(void) { return 0; }
static float answer_zero_float(void) { return 0.0f; }
static const char *answer_empty(void) { return ""; }
static void *answer(int kind)
{
    switch (kind) {
    case 1: return (void *)answer_zero_float;
    case 2: return (void *)compositor_failed;
    case 3: return (void *)answer_empty;
    default: return (void *)answer_zero;
    }
}

static void *added_method(Candidate *c, int slot, int table)
{
    switch (c->family) {
    case SYSTEM:
        if (slot == NEW_SYSTEM_022_POLLNEXTEVENTWITHPOSEANDOVERLAYS) return table ? (void *)poll_with_overlays_table : (void *)poll_with_overlays;
        return (void *)answer_zero;
    case COMPOSITOR:
        return (void *)compositor_failed;
    default:
        if (slot == NEW_OVERLAY_027_CREATESUBVIEWOVERLAY) return table ? (void *)create_subview_table : (void *)create_subview;
        return (void *)overlay_failed;
    }
}

static void overrides(Candidate *c, void **slots)
{
    const short *slot; const u8 *kind; int n;
    switch (c->family) {
    case SYSTEM: slot = OVR_SYSTEM_SLOT; kind = OVR_SYSTEM_KIND; n = OVR_SYSTEM_COUNT; break;
    case COMPOSITOR: slot = OVR_COMPOSITOR_SLOT; kind = OVR_COMPOSITOR_KIND; n = OVR_COMPOSITOR_COUNT; break;
    default: slot = OVR_OVERLAY_SLOT; kind = OVR_OVERLAY_KIND; n = OVR_OVERLAY_COUNT; break;
    }
    for (int i = 0; i < n; i++) if (slot[i] < c->count) slots[slot[i]] = answer(kind[i]);
}

static void *adapt_object(Candidate *c, void *inner)
{
    if (!c->built) {
        for (int i = 0; i < c->count; i++) {
            int m = c->map[i];
            c->vtbl[i] = (m >= 0 && m < THUNKS) ? (void *)(shim_thunk_base + 16 * m) : added_method(c, i, 0);
        }
        overrides(c, c->vtbl);
        c->built = 1;
    }
    c->object.vtbl = c->vtbl;
    c->object.inner = inner;
    return &c->object;
}

static void *adapt_table(Candidate *c, void **inner)
{
    c->inner_table = inner;
    for (int i = 0; i < c->count; i++) {
        int m = c->map[i];
        c->table[i] = (m >= 0 && m < c->oldCount) ? inner[m] : added_method(c, i, 1);
    }
    overrides(c, c->table);
    return c->table;
}

__declspec(dllexport) void *VR_GetGenericInterface(const char *name, int *error)
{
    choose();
    if (backend_kind != OC) {
        if (fn[E_GETGENERICINTERFACE]) return ((GetInterfaceFn)fn[E_GETGENERICINTERFACE])(name, error); /* SteamVR: as it is */
        if (error) *error = 102; /* VRInitError_Init_VRClientDLLNotFound */
        return 0;
    }
    if (!name) return oc_get(name, error);
    int table = prefix(name, "FnTable:");
    const char *base = table ? name + 8 : name;
    for (int f = 0; f < COUNT(families); f++) {
        Family *family = &families[f];
        if (!same(base, family->want)) continue;
        if (oc_has(family->want, &family->has)) break; /* OpenComposite has it: pass through */
        for (int k = 0; k < 3 && family->candidates[k]; k++) {
            Candidate *c = family->candidates[k];
            if (!oc_has(c->old, &c->has)) continue;
            void *inner = oc_get(table ? c->oldTable : c->old, error);
            if (!inner) return 0;
            int *noted = table ? &c->notedTable : &c->noted;
            if (!*noted) { *noted = 1; note(" ", name, " -> "); note(c->old, table ? " (table);" : ";", 0); }
            return table ? adapt_table(c, (void **)inner) : adapt_object(c, inner);
        }
        break;
    }
    return oc_get(name, error);
}
