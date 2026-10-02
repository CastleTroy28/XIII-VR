/* Linux test of the built openvr_api.dll (Windows x64 code, Microsoft ABI):
 * loads the DLL image by hand (sections, relocations, kernel32/advapi32
 * imports, all faked here). Scenarios (argv[2]):
 *   oc / oc027  the default OpenXR runtime is Pimax: the fake OpenComposite
 *               (IVRSystem_022, IVRCompositor_028 or _027, IVROverlay_027, like
 *               OpenComposite of 2025) is loaded, and every slot of the adapters
 *               the game gets for IVRSystem_023 / IVRCompositor_029 /
 *               IVROverlay_028 is checked, in C++ form (this = the adapter) and
 *               FnTable form;
 *   steam       the default is SteamVR: Valve's openvr_api_valve.dll, untouched;
 *   xrenv       XR_RUNTIME_JSON points at SteamVR (the registry at Pimax): SteamVR;
 *   forced      Pimax is the default but the setting says SteamVR: SteamVR;
 *   nooc        Pimax is the default, OpenComposite not installed: SteamVR;
 *   none        neither DLL: the game gets "installation not found".
 * Build and run: test.sh
 */
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <sys/mman.h>
#include "adapters.inc"

#define MS __attribute__((ms_abi))
typedef uint64_t u64; typedef uint32_t u32; typedef uint16_t u16; typedef uint8_t u8;
static int failures;
#define CHECK(c, ...) do { if (!(c)) { failures++; printf("FAIL %s:%d ", __FILE__, __LINE__); printf(__VA_ARGS__); printf("\n"); } } while (0)

/* ---- fake OpenComposite ---- */
static u8 fake_image[0x3000];
static const char *oc_versions[] = { "IVRSystem_022", "IVRCompositor_028", "IVROverlay_027", "IVRChaperone_004" };
static void build_fake_image(void)
{
    u8 *b = fake_image; memset(b, 0, sizeof fake_image);
    b[0] = 'M'; b[1] = 'Z'; *(u32 *)(b + 0x3C) = 0x80;
    u8 *nt = b + 0x80; *(u32 *)nt = 0x4550; *(u16 *)(nt + 6) = 1; *(u16 *)(nt + 20) = 0xF0;
    *(u32 *)(nt + 24 + 56) = sizeof fake_image;
    u8 *sec = nt + 24 + 0xF0; memcpy(sec, ".rdata", 6);
    *(u32 *)(sec + 8) = 0x1000; *(u32 *)(sec + 12) = 0x1000; *(u32 *)(sec + 16) = 0x1000; *(u32 *)(sec + 36) = 0x40000040;
    char *p = (char *)b + 0x1000;
    /* noise that must not count: a longer name and a version without NUL right after */
    strcpy(p, "FnTable:IVRSystem_0230"); p += 24;
    for (int i = 0; i < 4; i++) { strcpy(p, oc_versions[i]); p += strlen(p) + 1; }
}
static int last_slot, last_table; static void *last_self; static u64 last_args[5];
#define SLOT(n) \
    static MS u64 cpp_##n(void *self, u64 a, u64 b, u64 c, u64 d, u64 e) { last_slot = n; last_table = 0; last_self = self; last_args[0]=a; last_args[1]=b; last_args[2]=c; last_args[3]=d; last_args[4]=e; return 1000 + n; } \
    static MS u64 tab_##n(u64 a, u64 b, u64 c, u64 d, u64 e) { last_slot = n; last_table = 1; last_self = 0; last_args[0]=a; last_args[1]=b; last_args[2]=c; last_args[3]=d; last_args[4]=e; return 2000 + n; }
#define S10(t) SLOT(t##0) SLOT(t##1) SLOT(t##2) SLOT(t##3) SLOT(t##4) SLOT(t##5) SLOT(t##6) SLOT(t##7) SLOT(t##8) SLOT(t##9)
SLOT(0) SLOT(1) SLOT(2) SLOT(3) SLOT(4) SLOT(5) SLOT(6) SLOT(7) SLOT(8) SLOT(9)
S10(1) S10(2) S10(3) S10(4) S10(5) S10(6) S10(7)
SLOT(80) SLOT(81)
#define R10(p,t) p##_##t##0, p##_##t##1, p##_##t##2, p##_##t##3, p##_##t##4, p##_##t##5, p##_##t##6, p##_##t##7, p##_##t##8, p##_##t##9
static void *cpp_vtbl[82] = { cpp_0, cpp_1, cpp_2, cpp_3, cpp_4, cpp_5, cpp_6, cpp_7, cpp_8, cpp_9, R10(cpp,1), R10(cpp,2), R10(cpp,3), R10(cpp,4), R10(cpp,5), R10(cpp,6), R10(cpp,7), cpp_80, cpp_81 };
static void *fn_table[82] = { tab_0, tab_1, tab_2, tab_3, tab_4, tab_5, tab_6, tab_7, tab_8, tab_9, R10(tab,1), R10(tab,2), R10(tab,3), R10(tab,4), R10(tab,5), R10(tab,6), R10(tab,7), tab_80, tab_81 };
typedef struct { void **vtbl; int id; } Object;
static Object oc_objects[4];
static char oc_last_request[128]; static int oc_requests;
static MS void *oc_get(const char *name, int *error)
{
    oc_requests++; snprintf(oc_last_request, sizeof oc_last_request, "%s", name);
    if (error) *error = 0;
    const char *base = strncmp(name, "FnTable:", 8) ? name : name + 8;
    for (int i = 0; i < 4; i++) if (!strcmp(base, oc_versions[i])) {
        oc_objects[i].vtbl = cpp_vtbl; oc_objects[i].id = i;
        return base == name ? (void *)&oc_objects[i] : (void *)fn_table;
    }
    if (error) *error = 105;
    return 0; /* the real one stops the game here */
}
/* ---- fake Valve openvr_api (SteamVR): every export records its name ---- */
static u8 fake_valve[16];
static char valve_called[64]; static u64 valve_arg0;
static int valve_object;
#define VALVE(name) static MS u64 valve_##name(u64 a, u64 b, u64 c, u64 d) { snprintf(valve_called, sizeof valve_called, "%s", #name); valve_arg0 = a; if (!strcmp(#name, "VR_GetGenericInterface")) return (u64)&valve_object; return 4242; }
VALVE(VR_GetGenericInterface) VALVE(VR_InitInternal2) VALVE(VR_IsHmdPresent) VALVE(VRHeadsetView) VALVE(VR_ShutdownInternal) VALVE(VR_IsInterfaceVersionValid) VALVE(LiquidVR)
static void *valve_export(const char *name)
{
#define V(n) if (!strcmp(name, #n)) return (void *)valve_##n;
    V(VR_GetGenericInterface) V(VR_InitInternal2) V(VR_IsHmdPresent) V(VRHeadsetView) V(VR_ShutdownInternal) V(VR_IsInterfaceVersionValid) V(LiquidVR)
    return 0;
}
static MS u64 oc_init2(u64 a, u64 b, u64 c, u64 d) { if (a) *(int *)a = 0; return 77; }

/* ---- the scenario: what Windows has ---- */
static const char *scenario = "oc";
static const char *env_mode, *env_json, *registry;
static int has_valve_file = 1, has_oc_file = 1;
static char loaded_path[600]; static int loads;
static const char *const PIMAX = "C:\\Program Files\\Pimax\\Runtime\\PiOpenXR_64.json";
static const char *const STEAM = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\SteamVR\\steamxr_win64.json";
static const char *const DIR = "C:\\Game\\XIII_Data\\Plugins\\";
/* ---- kernel32 / advapi32 ---- */
static u8 *image;
static int ends(const char *s, const char *tail) { size_t a = strlen(s), b = strlen(tail); return a >= b && !strcmp(s + a - b, tail); }
static MS void *k_LoadLibraryExA(const char *name, void *file, u32 flags)
{
    loads++; snprintf(loaded_path, sizeof loaded_path, "%s", name);
    CHECK(flags == 8 && !strncmp(name, DIR, strlen(DIR)), "LoadLibraryExA(%s, %u): not by full path beside the switch", name, flags);
    if (ends(name, "openvr_api_oc.dll")) return has_oc_file ? fake_image : 0;
    if (ends(name, "openvr_api_valve.dll")) return has_valve_file ? fake_valve : 0;
    return 0;
}
static MS void *k_GetProcAddress(void *m, const char *name)
{
    if (m == fake_image) return !strcmp(name, "VR_GetGenericInterface") ? (void *)oc_get : !strcmp(name, "VR_InitInternal2") ? (void *)oc_init2 : 0;
    if (m == fake_valve) return valve_export(name);
    return 0;
}
static MS u32 k_GetModuleFileNameA(void *module, char *out, u32 size)
{
    CHECK(module == image, "GetModuleFileNameA asked about %p, not the switch itself", module);
    snprintf(out, size, "%sopenvr_api.dll", DIR); return (u32)strlen(out);
}
static MS u32 k_GetEnvironmentVariableA(const char *name, char *out, u32 size)
{
    const char *v = !strcmp(name, "XIIIVR_RUNTIME") ? env_mode : !strcmp(name, "XR_RUNTIME_JSON") ? env_json : 0;
    if (!v) return 0;
    if (strlen(v) + 1 > size) return (u32)strlen(v) + 1;
    strcpy(out, v); return (u32)strlen(v);
}
static MS u32 k_GetFileAttributesA(const char *name)
{
    if (ends(name, "openvr_api_oc.dll")) return has_oc_file ? 0x20 : 0xFFFFFFFFu;
    if (ends(name, "openvr_api_valve.dll")) return has_valve_file ? 0x20 : 0xFFFFFFFFu;
    return 0xFFFFFFFFu;
}
static MS long a_RegGetValueA(void *key, const char *subkey, const char *value, u32 flags, u32 *type, void *data, u32 *size)
{
    CHECK((u64)key == 0xFFFFFFFF80000002ull && !strcmp(subkey, "SOFTWARE\\Khronos\\OpenXR\\1") && !strcmp(value, "ActiveRuntime"), "registry: %p %s %s", key, subkey, value);
    if (!registry) return 2;
    if (strlen(registry) + 1 > *size) return 234;
    strcpy((char *)data, registry); *size = (u32)strlen(registry) + 1; return 0;
}

/* ---- PE loader ---- */
static u32 export_dir;
static void *load(const char *path)
{
    FILE *f = fopen(path, "rb"); if (!f) { perror(path); exit(2); }
    fseek(f, 0, SEEK_END); long n = ftell(f); fseek(f, 0, SEEK_SET);
    u8 *file = malloc(n); if (fread(file, 1, n, f) != (size_t)n) exit(2); fclose(f);
    u8 *nt = file + *(u32 *)(file + 0x3C);
    u16 sections = *(u16 *)(nt + 6), optional = *(u16 *)(nt + 20);
    u8 *opt = nt + 24; u64 preferred = *(u64 *)(opt + 24); u32 size = *(u32 *)(opt + 56), headers = *(u32 *)(opt + 60);
    image = mmap(0, size, PROT_READ | PROT_WRITE | PROT_EXEC, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    memcpy(image, file, headers);
    u8 *sec = opt + optional;
    for (int i = 0; i < sections; i++) { u8 *h = sec + 40 * i; memcpy(image + *(u32 *)(h + 12), file + *(u32 *)(h + 20), *(u32 *)(h + 16)); }
    u8 *dirs = opt + 112;
    export_dir = *(u32 *)(dirs + 0);
    u32 imports = *(u32 *)(dirs + 8), relocs = *(u32 *)(dirs + 40), relocs_size = *(u32 *)(dirs + 44);
    u64 delta = (u64)image - preferred; int fixed = 0;
    for (u32 at = 0; at < relocs_size;) {
        u32 page = *(u32 *)(image + relocs + at), block = *(u32 *)(image + relocs + at + 4);
        for (u32 k = 8; k < block; k += 2) { u16 e = *(u16 *)(image + relocs + at + k); if ((e >> 12) == 10) { *(u64 *)(image + page + (e & 0xFFF)) += delta; fixed++; } else CHECK((e >> 12) == 0, "relocation type %d", e >> 12); }
        at += block;
    }
    for (u8 *d = image + imports; *(u32 *)(d + 12); d += 20) {
        const char *dll = (char *)image + *(u32 *)(d + 12);
        CHECK(!strcasecmp(dll, "kernel32.dll") || !strcasecmp(dll, "advapi32.dll"), "imports from %s", dll);
        u64 *lookup = (u64 *)(image + *(u32 *)(d + 0)), *iat = (u64 *)(image + *(u32 *)(d + 16));
        for (; *lookup; lookup++, iat++) {
            const char *fn = (char *)image + (u32)*lookup + 2;
            *iat = !strcmp(fn, "LoadLibraryExA") ? (u64)k_LoadLibraryExA : !strcmp(fn, "GetProcAddress") ? (u64)k_GetProcAddress
                 : !strcmp(fn, "GetModuleFileNameA") ? (u64)k_GetModuleFileNameA : !strcmp(fn, "GetEnvironmentVariableA") ? (u64)k_GetEnvironmentVariableA
                 : !strcmp(fn, "GetFileAttributesA") ? (u64)k_GetFileAttributesA : !strcmp(fn, "RegGetValueA") ? (u64)a_RegGetValueA : 0;
            CHECK(*iat, "unknown import %s", fn);
        }
    }
    printf("loaded %s: %u bytes, %d relocations\n", path, size, fixed);
    return image;
}
static void *export(const char *name)
{
    u8 *e = image + export_dir; u32 names = *(u32 *)(e + 24);
    u32 *funcs = (u32 *)(image + *(u32 *)(e + 28)), *nameptr = (u32 *)(image + *(u32 *)(e + 32)); u16 *ords = (u16 *)(image + *(u32 *)(e + 36));
    for (u32 i = 0; i < names; i++) if (!strcmp((char *)image + nameptr[i], name)) return image + funcs[ords[i]];
    return 0;
}

typedef MS void *(*GetInterface)(const char *, int *);
typedef MS u64 (*Cpp5)(void *, u64, u64, u64, u64, u64);
typedef MS u64 (*Tab5)(u64, u64, u64, u64, u64);
typedef MS float (*CppF)(void *);
typedef MS float (*TabF)(void);

static void check_family(GetInterface get, const char *want, const char *old, const short *map, int count,
                         const short *ovr_slot, const u8 *ovr_kind, int ovr_count)
{
    char name[64];
    for (int table = 0; table < 2; table++) {
        snprintf(name, sizeof name, "%s%s", table ? "FnTable:" : "", want);
        int error = -1; oc_last_request[0] = 0;
        void *p = get(name, &error);
        char expect[64]; snprintf(expect, sizeof expect, "%s%s", table ? "FnTable:" : "", old);
        CHECK(p && error == 0, "%s: got %p error %d", name, p, error);
        CHECK(!strcmp(oc_last_request, expect), "%s asked OpenComposite for %s", name, oc_last_request);
        if (!p) continue;
        void **slots = table ? (void **)p : *(void ***)p;
        for (int i = 0; i < count; i++) {
            int override = -1;
            for (int k = 0; k < ovr_count; k++) if (ovr_slot[k] == i) override = ovr_kind[k];
            last_slot = -1; last_self = (void *)1;
            u64 r;
            if (override == 1) {
                float f = table ? ((TabF)slots[i])() : ((CppF)slots[i])(p);
                CHECK(f == 0.0f && last_slot == -1, "%s slot %d: float override gave %f, reached old slot %d", name, i, f, last_slot);
                continue;
            }
            /* the 4th and 5th arguments are pointers: an added method may write through them */
            u64 fourth = 88, overlay = 77, d = (u64)&fourth;
            if (table) r = ((Tab5)slots[i])(11, 22, 33, d, (u64)&overlay);
            else r = ((Cpp5)slots[i])(p, 11, 22, 33, d, (u64)&overlay);
            if (override >= 0) {
                CHECK(last_slot == -1, "%s slot %d: override reached old slot %d", name, i, last_slot);
                if (override == 2) CHECK((int)r == 1, "%s slot %d: override returned %llu, not RequestFailed", name, i, (unsigned long long)r);
                else if (override == 3) CHECK(r && *(char *)r == 0, "%s slot %d: override is not \"\"", name, i);
                else CHECK((int)r == 0, "%s slot %d: override returned %llu", name, i, (unsigned long long)r);
                continue;
            }
            if (map[i] >= 0) {
                CHECK(last_slot == map[i], "%s slot %d reached old slot %d, not %d", name, i, last_slot, map[i]);
                CHECK(last_table == table, "%s slot %d: wrong table kind", name, i);
                CHECK(r == (u64)((table ? 2000 : 1000) + map[i]), "%s slot %d returned %llu", name, i, (unsigned long long)r);
                if (!table) CHECK(last_self == (void *)&oc_objects[0] || last_self == (void *)&oc_objects[1] || last_self == (void *)&oc_objects[2], "%s slot %d: this %p is not OpenComposite's object", name, i, last_self);
                CHECK(last_args[0] == 11 && last_args[1] == 22 && last_args[2] == 33 && last_args[3] == d && last_args[4] == (u64)&overlay, "%s slot %d: arguments changed", name, i);
                continue;
            }
            /* a method the old version lacks */
            if (!strcmp(want, "IVRSystem_023")) {
                CHECK(i == NEW_SYSTEM_022_POLLNEXTEVENTWITHPOSEANDOVERLAYS, "unexpected added slot %d", i);
                CHECK(last_slot == OLD_SYSTEM_022_POLLNEXTEVENTWITHPOSE, "%s: PollNextEventWithPoseAndOverlays reached %d", name, last_slot);
                CHECK(overlay == 0, "%s: overlay handle not cleared", name);
                CHECK(last_args[0] == 11 && last_args[1] == 22 && last_args[2] == 33 && last_args[3] == d, "%s: poll arguments changed", name);
                CHECK((u8)r == (u8)((table ? 2000 : 1000) + OLD_SYSTEM_022_POLLNEXTEVENTWITHPOSE), "%s: poll result %llu", name, (unsigned long long)r);
            } else if (!strcmp(want, "IVRCompositor_029")) {
                CHECK(last_slot == -1 && (int)r == 1, "%s slot %d: added method gave %llu / reached %d", name, i, (unsigned long long)r, last_slot);
            } else {
                CHECK(last_slot == -1 && (int)r == 23, "%s slot %d: added method gave %llu / reached %d", name, i, (unsigned long long)r, last_slot);
                /* CreateSubviewOverlay(parent, key, name, &handle): the handle is the 4th argument */
                if (i == NEW_OVERLAY_027_CREATESUBVIEWOVERLAY) CHECK(fourth == 0, "%s: subview handle not cleared", name);
                else CHECK(fourth == 88, "%s slot %d wrote through an argument", name, i);
            }
        }
        printf("ok %s -> %s (%d slots)\n", name, expect, count);
    }
}

typedef MS u64 (*Pass4)(u64, u64, u64, u64);
typedef MS int (*Decide)(const char *, const char *, int, int);
static const char *report_now(void) { return ((MS const char *(*)(void))export("XIIIVR_ShimReport"))(); }
/* SteamVR and the fallbacks: Valve's DLL untouched, or nothing. */
static int steam_scenarios(GetInterface get)
{
    int error = -1;
    if (!strcmp(scenario, "none")) {
        int init_error = -1;
        u64 r = ((Pass4)export("VR_InitInternal2"))((u64)&init_error, 1, 0, 0);
        CHECK(r == 0 && init_error == 100, "no runtime DLL: VR_InitInternal2 gave %llu, error %d (not 100)", (unsigned long long)r, init_error);
        CHECK(!get("IVRSystem_023", &error) && error == 102, "no runtime DLL: an interface or no error (%d)", error);
        CHECK(((Pass4)export("VRHeadsetView"))(0, 0, 0, 0) == 0 && ((Pass4)export("VR_IsHmdPresent"))(0, 0, 0, 0) == 0, "no runtime DLL: a headset reported");
        const char *symbol = (const char *)((Pass4)export("VR_GetVRInitErrorAsSymbol"))(100, 0, 0, 0);
        CHECK(symbol && strstr(symbol, "InstallationNotFound"), "no runtime DLL: no error text");
        CHECK(strstr(report_now(), "no OpenVR runtime DLL found"), "report: %s", report_now());
        return 0;
    }
    void *p = get("IVRSystem_023", &error);
    CHECK(p == &valve_object && !strcmp(valve_called, "VR_GetGenericInterface") && !strcmp((const char *)valve_arg0, "IVRSystem_023"), "SteamVR: IVRSystem_023 not handed to Valve's DLL as it is");
    CHECK(get("FnTable:IVRCompositor_029", &error) == &valve_object && !strcmp((const char *)valve_arg0, "FnTable:IVRCompositor_029"), "SteamVR: the mod's FnTable not handed to Valve's DLL");
    CHECK(oc_requests == 0, "SteamVR: OpenComposite asked");
    CHECK(((Pass4)export("VRHeadsetView"))(0, 0, 0, 0) == 4242 && !strcmp(valve_called, "VRHeadsetView"), "SteamVR: VRHeadsetView not Valve's");
    CHECK(((Pass4)export("LiquidVR"))(0, 0, 0, 0) == 4242, "SteamVR: LiquidVR not Valve's");
    int init_error = -1;
    CHECK(((Pass4)export("VR_InitInternal2"))((u64)&init_error, 1, 0, 0) == 4242 && valve_arg0 == (u64)&init_error, "SteamVR: VR_InitInternal2 arguments changed");
    CHECK(((Pass4)export("VR_GetInitToken"))(0, 0, 0, 0) == 0, "an export Valve's fake lacks did not answer 0");
    CHECK(ends(loaded_path, "openvr_api_valve.dll") && loads == 1, "SteamVR: loaded %s (%d loads)", loaded_path, loads);
    const char *report = report_now();
    CHECK(strstr(report, "-> SteamVR (openvr_api_valve.dll)") != 0, "report: %s", report);
    if (!strcmp(scenario, "nooc")) CHECK(strstr(report, "OpenComposite not installed") != 0, "report does not say OpenComposite is missing: %s", report);
    if (!strcmp(scenario, "forced")) CHECK(strstr(report, "setting SteamVR") != 0, "report: %s", report);
    if (!strcmp(scenario, "xrenv")) CHECK(strstr(report, "(XR_RUNTIME_JSON)") != 0, "report: %s", report);
    printf("report: %s\n", report);
    return 0;
}

int main(int argc, char **argv)
{
    setvbuf(stdout, 0, _IONBF, 0);
    scenario = argc > 2 ? argv[2] : "oc";
    int old27 = !strcmp(scenario, "oc027"); /* an older OpenComposite: IVRCompositor_027 */
    if (old27) oc_versions[1] = "IVRCompositor_027";
    registry = PIMAX;
    if (!strcmp(scenario, "steam")) registry = STEAM;
    else if (!strcmp(scenario, "xrenv")) env_json = STEAM;
    else if (!strcmp(scenario, "forced")) env_mode = "SteamVR";
    else if (!strcmp(scenario, "nooc")) has_oc_file = 0;
    else if (!strcmp(scenario, "none")) has_oc_file = has_valve_file = 0;
    else if (strcmp(scenario, "oc") && !old27) { printf("unknown scenario %s\n", scenario); return 2; }
    build_fake_image();
    load(argc > 1 ? argv[1] : "../../OpenXR/openvr_api.dll");
    GetInterface get = (GetInterface)export("VR_GetGenericInterface");
    CHECK(get, "no VR_GetGenericInterface export");
    CHECK(export("VRHeadsetView") && export("XIIIVR_ShimReport") && export("LiquidVR") && export("VR_InitInternal2"), "exports missing");
    if (!get) return 1;
    /* the decision itself */
    Decide decide = (Decide)export("XIIIVR_Decide");
    CHECK(decide && decide("auto", PIMAX, 1, 1) == 2 && decide("Auto", STEAM, 1, 1) == 1 && decide("auto", "", 1, 1) == 1, "auto: Pimax -> OpenComposite, SteamVR or none -> SteamVR");
    CHECK(decide("auto", "C:\\Program Files\\Oculus\\Support\\oculus-runtime\\oculus_openxr_64.json", 1, 1) == 2 && decide("auto", "D:\\SteamLibrary\\steamapps\\common\\SteamVR\\steamxr_win64.json", 1, 1) == 1, "Oculus -> OpenComposite, SteamVR in another library -> SteamVR");
    CHECK(decide("steamvr", PIMAX, 1, 1) == 1 && decide("OpenXR", STEAM, 1, 1) == 2, "the setting does not win");
    CHECK(decide("auto", PIMAX, 1, 0) == 1 && decide("openxr", PIMAX, 1, 0) == 1 && decide("steamvr", STEAM, 0, 1) == 2 && decide("auto", PIMAX, 0, 0) == 0, "a missing DLL: the other one, or none");
    if (strcmp(scenario, "oc") && !old27) { steam_scenarios(get); if (failures) printf("FAILED: %d (%s)\n", failures, scenario); else printf("ALL OK (%s)\n", scenario); return failures != 0; }
    /* OpenComposite: VRHeadsetView (it lacks it) answers NULL; its own exports are its own */
    CHECK(((Pass4)export("VRHeadsetView"))(0, 0, 0, 0) == 0, "OpenComposite: VRHeadsetView not NULL");
    int init_error = -1;
    CHECK(((Pass4)export("VR_InitInternal2"))((u64)&init_error, 1, 0, 0) == 77 && init_error == 0, "OpenComposite: VR_InitInternal2 not its own");
    CHECK(ends(loaded_path, "openvr_api_oc.dll") && loads == 1, "OpenComposite: loaded %s (%d loads)", loaded_path, loads);
    check_family(get, "IVRSystem_023", "IVRSystem_022", MAP_SYSTEM_022, 47, OVR_SYSTEM_SLOT, OVR_SYSTEM_KIND, OVR_SYSTEM_COUNT);
    if (old27) check_family(get, "IVRCompositor_029", "IVRCompositor_027", MAP_COMPOSITOR_027, 53, OVR_COMPOSITOR_SLOT, OVR_COMPOSITOR_KIND, OVR_COMPOSITOR_COUNT);
    else check_family(get, "IVRCompositor_029", "IVRCompositor_028", MAP_COMPOSITOR_028, 53, OVR_COMPOSITOR_SLOT, OVR_COMPOSITOR_KIND, OVR_COMPOSITOR_COUNT);
    check_family(get, "IVROverlay_028", "IVROverlay_027", MAP_OVERLAY_027, 82, OVR_OVERLAY_SLOT, OVR_OVERLAY_KIND, OVR_OVERLAY_COUNT);
    /* versions OpenComposite has: passed through untouched */
    int error = -1; void *p = get("IVRChaperone_004", &error);
    CHECK(p == &oc_objects[3] && !strcmp(oc_last_request, "IVRChaperone_004"), "IVRChaperone_004 not passed through");
    p = get("IVRSystem_022", &error);
    CHECK(p == &oc_objects[0], "IVRSystem_022 not passed through");
    p = get("FnTable:IVRSystem_022", &error);
    CHECK(p == fn_table, "FnTable:IVRSystem_022 not passed through");
    /* an unknown version still goes to OpenComposite (its own message) */
    p = get("IVRSystem_024", &error);
    CHECK(!p && !strcmp(oc_last_request, "IVRSystem_024"), "IVRSystem_024 not passed through");
    /* the same adapter again (the game asks more than once) */
    void *a = get("IVRSystem_023", &error), *b = get("IVRSystem_023", &error);
    CHECK(a && a == b, "IVRSystem_023 adapter changed between calls");
    const char *report = ((MS const char *(*)(void))export("XIIIVR_ShimReport"))();
    printf("report: %s\n", report);
    CHECK(strstr(report, "-> OpenComposite (openvr_api_oc.dll)") && strstr(report, "PiOpenXR_64.json"), "report does not name the runtime");
    CHECK(strstr(report, "IVRSystem_023 -> IVRSystem_022") && strstr(report, old27 ? "FnTable:IVRCompositor_029 -> IVRCompositor_027 (table)" : "FnTable:IVRCompositor_029 -> IVRCompositor_028 (table)"), "report incomplete");
    printf(failures ? "FAILED: %d\n" : "ALL OK\n", failures);
    return failures != 0;
}
