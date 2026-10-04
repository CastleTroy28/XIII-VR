/* Linux test of the built xiii_openxr.dll (Windows x64 code, Microsoft ABI):
 * loads the DLL image by hand and plays Unity's OpenXR plugin against a fake
 * OpenXR runtime: the plugin asks the helper's xrGetInstanceProcAddr for its
 * functions, creates the instance and the session, begins it, waits for
 * frames and polls events; the checks follow what the helper does beside it
 * (the action set, the bindings of every controller kind, the attach, the
 * buttons, the poses at the next frame's time with the grip-to-SteamVR
 * transforms, the eyes, the vibration). Build and run: test.sh
 */
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <math.h>
#include <sys/mman.h>
#define XR_NO_PROTOTYPES
#include "openxr/openxr.h"

#define MS __attribute__((ms_abi))
typedef uint64_t u64; typedef uint32_t u32; typedef uint16_t u16; typedef uint8_t u8;
static int failures;
#define CHECK(c, ...) do { if (!(c)) { failures++; printf("FAIL %s:%d ", __FILE__, __LINE__); printf(__VA_ARGS__); printf("\n"); } } while (0)

/* ---- PE loader (no imports expected) ---- */
static u8 *image; static u32 export_dir;
static void load(const char *path)
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
    CHECK(imports == 0, "the helper imports something (it must not)");
    u64 delta = (u64)image - preferred;
    for (u32 at = 0; at < relocs_size;) {
        u32 page = *(u32 *)(image + relocs + at), block = *(u32 *)(image + relocs + at + 4);
        for (u32 k = 8; k < block; k += 2) { u16 e = *(u16 *)(image + relocs + at + k); if ((e >> 12) == 10) *(u64 *)(image + page + (e & 0xFFF)) += delta; }
        at += block;
    }
}
static void *export(const char *name)
{
    u8 *e = image + export_dir; u32 names = *(u32 *)(e + 24);
    u32 *funcs = (u32 *)(image + *(u32 *)(e + 28)), *nameptr = (u32 *)(image + *(u32 *)(e + 32)); u16 *ords = (u16 *)(image + *(u32 *)(e + 36));
    for (u32 i = 0; i < names; i++) if (!strcmp((char *)image + nameptr[i], name)) return image + funcs[ords[i]];
    return 0;
}

/* ---- the fake runtime ---- */
#define INSTANCE ((XrInstance)0x1111)
#define SESSION ((XrSession)0x2222)
static char paths[256][160]; static int path_count;
static MS XrResult f_StringToPath(XrInstance inst, const char *s, XrPath *out)
{
    CHECK(inst == INSTANCE, "xrStringToPath on another instance");
    for (int i = 0; i < path_count; i++) if (!strcmp(paths[i], s)) { *out = i + 1; return XR_SUCCESS; }
    if (strstr(s, "/hp/")) return XR_ERROR_PATH_UNSUPPORTED; /* this runtime has no HP controller extension */
    snprintf(paths[path_count], 160, "%s", s); *out = ++path_count; return XR_SUCCESS;
}
static const char *path_name(XrPath p) { return p >= 1 && p <= (XrPath)path_count ? paths[p - 1] : "?"; }
static MS XrResult f_PathToString(XrInstance inst, XrPath p, uint32_t cap, uint32_t *n, char *out)
{
    const char *s = path_name(p); *n = (uint32_t)strlen(s) + 1; if (cap < *n) return XR_ERROR_SIZE_INSUFFICIENT; strcpy(out, s); return XR_SUCCESS;
}
static int sets_made, actions_made; static char action_names[16][64]; static XrAction action_handles[16];
static MS XrResult f_CreateActionSet(XrInstance inst, const XrActionSetCreateInfo *ci, XrActionSet *out)
{ sets_made++; CHECK(!strcmp(ci->actionSetName, "xiii_vr"), "action set name %s", ci->actionSetName); *out = (XrActionSet)0x3333; return XR_SUCCESS; }
static MS XrResult f_DestroyActionSet(XrActionSet s) { return XR_SUCCESS; }
static MS XrResult f_CreateAction(XrActionSet s, const XrActionCreateInfo *ci, XrAction *out)
{
    CHECK(ci->countSubactionPaths == 2 && !strcmp(path_name(ci->subactionPaths[0]), "/user/hand/left") && !strcmp(path_name(ci->subactionPaths[1]), "/user/hand/right"), "subaction paths");
    snprintf(action_names[actions_made], 64, "%s", ci->actionName);
    *out = action_handles[actions_made] = (XrAction)(uintptr_t)(0x4000 + actions_made); actions_made++; return XR_SUCCESS;
}
static const char *action_name(XrAction a) { for (int i = 0; i < actions_made; i++) if (action_handles[i] == a) return action_names[i]; return "?"; }
static char suggested[8][64][200]; static char suggested_profile[8][100]; static int suggested_count[8], profiles_suggested;
static MS XrResult f_Suggest(XrInstance inst, const XrInteractionProfileSuggestedBinding *sb)
{
    int p = profiles_suggested++;
    snprintf(suggested_profile[p], 100, "%s", path_name(sb->interactionProfile));
    for (u32 k = 0; k < sb->countSuggestedBindings && k < 64; k++)
        snprintf(suggested[p][k], 200, "%s=%s", action_name(sb->suggestedBindings[k].action), path_name(sb->suggestedBindings[k].binding));
    suggested_count[p] = (int)sb->countSuggestedBindings;
    return XR_SUCCESS;
}
static int has_binding(const char *profile, const char *binding)
{
    for (int p = 0; p < profiles_suggested; p++) if (strstr(suggested_profile[p], profile))
        for (int k = 0; k < suggested_count[p]; k++) if (!strcmp(suggested[p][k], binding)) return 1;
    return 0;
}
static int attaches, attached_sets;
static MS XrResult f_Attach(XrSession s, const XrSessionActionSetsAttachInfo *ai)
{ attaches++; attached_sets = (int)ai->countActionSets; CHECK(s == SESSION, "attach to another session"); return attaches > 1 ? XR_ERROR_ACTIONSETS_ALREADY_ATTACHED : XR_SUCCESS; }
static int spaces_made;
typedef struct { int kind; XrPath hand; XrAction action; } FakeSpace; /* kind 1 view, 2 stage, 3 local, 4 action */
static FakeSpace fake_spaces[16];
static MS XrResult f_CreateActionSpace(XrSession s, const XrActionSpaceCreateInfo *ci, XrSpace *out)
{ fake_spaces[spaces_made] = (FakeSpace){ 4, ci->subactionPath, ci->action }; *out = (XrSpace)(uintptr_t)(0x5000 + spaces_made++); return XR_SUCCESS; }
static MS XrResult f_CreateReferenceSpace(XrSession s, const XrReferenceSpaceCreateInfo *ci, XrSpace *out)
{
    int kind = ci->referenceSpaceType == XR_REFERENCE_SPACE_TYPE_VIEW ? 1 : ci->referenceSpaceType == XR_REFERENCE_SPACE_TYPE_STAGE ? 2 : 3;
    fake_spaces[spaces_made] = (FakeSpace){ kind, 0, 0 }; *out = (XrSpace)(uintptr_t)(0x5000 + spaces_made++); return XR_SUCCESS;
}
static MS XrResult f_EnumerateReferenceSpaces(XrSession s, uint32_t cap, uint32_t *n, XrReferenceSpaceType *out)
{ *n = 3; if (cap >= 3) { out[0] = XR_REFERENCE_SPACE_TYPE_VIEW; out[1] = XR_REFERENCE_SPACE_TYPE_LOCAL; out[2] = XR_REFERENCE_SPACE_TYPE_STAGE; } return XR_SUCCESS; }
static MS XrResult f_DestroySpace(XrSpace s) { return XR_SUCCESS; }
static int syncs;
static int last_sync_count; static XrActionSet last_sync_set;
static MS XrResult f_Sync(XrSession s, const XrActionsSyncInfo *si)
{ syncs++; last_sync_count = (int)si->countActiveActionSets; last_sync_set = si->countActiveActionSets ? si->activeActionSets[si->countActiveActionSets - 1].actionSet : 0; return XR_SUCCESS; }
static int right(XrPath p) { return !strcmp(path_name(p), "/user/hand/right"); }
static MS XrResult f_GetFloat(XrSession s, const XrActionStateGetInfo *gi, XrActionStateFloat *st)
{
    const char *a = action_name(gi->action); st->isActive = 1;
    st->currentState = !strcmp(a, "trigger") ? (right(gi->subactionPath) ? .9f : .3f) : (right(gi->subactionPath) ? .1f : .8f);
    return XR_SUCCESS;
}
static MS XrResult f_GetBool(XrSession s, const XrActionStateGetInfo *gi, XrActionStateBoolean *st)
{
    const char *a = action_name(gi->action); st->isActive = 1;
    st->currentState = right(gi->subactionPath) ? !strcmp(a, "primary") : (!strcmp(a, "secondary") || !strcmp(a, "thumbstick_click"));
    return XR_SUCCESS;
}
static MS XrResult f_GetVec2(XrSession s, const XrActionStateGetInfo *gi, XrActionStateVector2f *st)
{ st->isActive = 1; st->currentState.x = right(gi->subactionPath) ? .5f : -.25f; st->currentState.y = right(gi->subactionPath) ? 0 : 1; return XR_SUCCESS; }
static XrTime located_at; static XrSpace located_base;
static XrPosef grip_pose[2] = { { { 0.1f, 0.2f, 0.3f, 0.9273618f }, { -0.2f, 1.1f, -0.4f } }, { { -0.05f, 0.3f, 0.1f, 0.9473648f }, { 0.25f, 1.0f, -0.35f } } };
static XrPosef head_pose = { { 0, 0.3826834f, 0, 0.9238795f }, { 0.1f, 1.7f, 0.2f } };
static MS XrResult f_LocateSpace(XrSpace space, XrSpace base, XrTime t, XrSpaceLocation *l)
{
    located_at = t; located_base = base;
    FakeSpace *fs = &fake_spaces[(uintptr_t)space - 0x5000];
    l->locationFlags = XR_SPACE_LOCATION_POSITION_VALID_BIT | XR_SPACE_LOCATION_ORIENTATION_VALID_BIT;
    if (fs->kind == 1) l->pose = head_pose;
    else if (fs->kind == 4 && !strcmp(action_name(fs->action), "grip_pose")) l->pose = grip_pose[right(fs->hand)];
    else if (fs->kind == 4) { l->pose = grip_pose[right(fs->hand)]; l->pose.position.z -= 0.05f; }
    else l->locationFlags = 0;
    return XR_SUCCESS;
}
static MS XrResult f_LocateViews(XrSession s, const XrViewLocateInfo *vi, XrViewState *vs, uint32_t cap, uint32_t *n, XrView *views)
{
    *n = 2; vs->viewStateFlags = XR_VIEW_STATE_ORIENTATION_VALID_BIT | XR_VIEW_STATE_POSITION_VALID_BIT;
    if (!cap || !views) return XR_SUCCESS;
    for (int e = 0; e < 2; e++) {
        views[e].pose = (XrPosef){ { 0, 0, 0, 1 }, { e ? 0.032f : -0.032f, 0, 0 } };
        views[e].fov = (XrFovf){ e ? -0.8f : -0.9f, e ? 0.9f : 0.8f, 0.85f, -0.95f };
    }
    return XR_SUCCESS;
}
static XrDuration haptic_duration; static float haptic_amplitude; static XrPath haptic_hand; static int haptics;
static MS XrResult f_Haptic(XrSession s, const XrHapticActionInfo *hi, const XrHapticBaseHeader *h)
{
    const XrHapticVibration *v = (const XrHapticVibration *)h; haptics++;
    CHECK(!strcmp(action_name(hi->action), "haptic") && v->type == XR_TYPE_HAPTIC_VIBRATION && v->frequency == XR_FREQUENCY_UNSPECIFIED, "haptic action");
    haptic_duration = v->duration; haptic_amplitude = v->amplitude; haptic_hand = hi->subactionPath; return XR_SUCCESS;
}
static MS XrResult f_StopHaptic(XrSession s, const XrHapticActionInfo *hi) { return XR_SUCCESS; }
static const char *current_profile = "/interaction_profiles/oculus/touch_controller";
static MS XrResult f_CurrentProfile(XrSession s, XrPath hand, XrInteractionProfileState *st)
{ XrPath p; f_StringToPath(INSTANCE, current_profile, &p); st->interactionProfile = p; return XR_SUCCESS; }
static MS XrResult f_InstanceProperties(XrInstance i, XrInstanceProperties *p) { strcpy(p->runtimeName, "Fake Pimax OpenXR"); p->runtimeVersion = XR_MAKE_VERSION(1, 2, 3); return XR_SUCCESS; }
static MS XrResult f_SystemProperties(XrInstance i, XrSystemId id, XrSystemProperties *p) { strcpy(p->systemName, "Pimax Crystal"); return XR_SUCCESS; }
static MS XrResult f_CreateInstance(const XrInstanceCreateInfo *ci, XrInstance *out) { *out = INSTANCE; return XR_SUCCESS; }
static MS XrResult f_DestroyInstance(XrInstance i) { return XR_SUCCESS; }
static MS XrResult f_CreateSession(XrInstance i, const XrSessionCreateInfo *ci, XrSession *out) { *out = SESSION; return XR_SUCCESS; }
static MS XrResult f_DestroySession(XrSession s) { return XR_SUCCESS; }
static MS XrResult f_BeginSession(XrSession s, const XrSessionBeginInfo *bi) { return XR_SUCCESS; }
static MS XrResult f_WaitFrame(XrSession s, const XrFrameWaitInfo *wi, XrFrameState *fs) { fs->predictedDisplayTime = 1000000000; fs->predictedDisplayPeriod = 11111111; fs->shouldRender = 1; return XR_SUCCESS; }
static int events_left = 2;
static MS XrResult f_PollEvent(XrInstance i, XrEventDataBuffer *e)
{
    if (events_left == 2) { events_left--; XrEventDataSessionStateChanged *c = (XrEventDataSessionStateChanged *)e; c->type = XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED; c->session = SESSION; c->state = XR_SESSION_STATE_FOCUSED; return XR_SUCCESS; }
    if (events_left == 1) { events_left--; e->type = XR_TYPE_EVENT_DATA_INTERACTION_PROFILE_CHANGED; return XR_SUCCESS; }
    return XR_EVENT_UNAVAILABLE;
}
static int gipa_calls;
static MS XrResult f_gipa(XrInstance inst, const char *name, PFN_xrVoidFunction *fn)
{
    gipa_calls++;
#define F(n) if (!strcmp(name, "xr" #n)) { *fn = (PFN_xrVoidFunction)f_##n; return XR_SUCCESS; }
    F(CreateInstance) F(DestroyInstance) F(CreateSession) F(DestroySession) F(BeginSession) F(WaitFrame) F(PollEvent)
    F(StringToPath) F(PathToString) F(CreateActionSet) F(DestroyActionSet) F(CreateAction) F(Attach) F(CreateActionSpace)
    F(CreateReferenceSpace) F(EnumerateReferenceSpaces) F(DestroySpace) F(Sync) F(GetFloat) F(GetBool) F(GetVec2)
    F(LocateSpace) F(LocateViews) F(Haptic) F(StopHaptic) F(CurrentProfile) F(InstanceProperties) F(SystemProperties)
#undef F
    if (!strcmp(name, "xrSuggestInteractionProfileBindings")) { *fn = (PFN_xrVoidFunction)f_Suggest; return XR_SUCCESS; }
    if (!strcmp(name, "xrAttachSessionActionSets")) { *fn = (PFN_xrVoidFunction)f_Attach; return XR_SUCCESS; }
    if (!strcmp(name, "xrApplyHapticFeedback")) { *fn = (PFN_xrVoidFunction)f_Haptic; return XR_SUCCESS; }
    if (!strcmp(name, "xrStopHapticFeedback")) { *fn = (PFN_xrVoidFunction)f_StopHaptic; return XR_SUCCESS; }
    if (!strcmp(name, "xrGetActionStateFloat")) { *fn = (PFN_xrVoidFunction)f_GetFloat; return XR_SUCCESS; }
    if (!strcmp(name, "xrGetActionStateBoolean")) { *fn = (PFN_xrVoidFunction)f_GetBool; return XR_SUCCESS; }
    if (!strcmp(name, "xrGetActionStateVector2f")) { *fn = (PFN_xrVoidFunction)f_GetVec2; return XR_SUCCESS; }
    if (!strcmp(name, "xrSyncActions")) { *fn = (PFN_xrVoidFunction)f_Sync; return XR_SUCCESS; }
    if (!strcmp(name, "xrGetCurrentInteractionProfile")) { *fn = (PFN_xrVoidFunction)f_CurrentProfile; return XR_SUCCESS; }
    if (!strcmp(name, "xrGetInstanceProperties")) { *fn = (PFN_xrVoidFunction)f_InstanceProperties; return XR_SUCCESS; }
    if (!strcmp(name, "xrGetSystemProperties")) { *fn = (PFN_xrVoidFunction)f_SystemProperties; return XR_SUCCESS; }
    if (!strcmp(name, "xrGetInstanceProcAddr")) { *fn = (PFN_xrVoidFunction)f_gipa; return XR_SUCCESS; }
    *fn = 0; return XR_ERROR_FUNCTION_UNSUPPORTED;
}

/* ---- an independent grip-to-SteamVR check (4x4 matrices, like OpenComposite's glm) ---- */
typedef struct { double m[4][4]; } M; /* m[row][col], column vectors */
static M mul(M a, M b) { M r; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { r.m[i][j] = 0; for (int k = 0; k < 4; k++) r.m[i][j] += a.m[i][k] * b.m[k][j]; } return r; }
static M ident(void) { M r = { { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } } }; return r; }
static M trans(double x, double y, double z) { M r = ident(); r.m[0][3] = x; r.m[1][3] = y; r.m[2][3] = z; return r; }
static M rot(int axis, double deg)
{
    double a = deg * M_PI / 180, c = cos(a), s = sin(a); M r = ident();
    int i = (axis + 1) % 3, j = (axis + 2) % 3;
    r.m[i][i] = c; r.m[i][j] = -s; r.m[j][i] = s; r.m[j][j] = c; return r;
}
static M from_pose(XrPosef p)
{
    double x = p.orientation.x, y = p.orientation.y, z = p.orientation.z, w = p.orientation.w;
    M r = { { { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), p.position.x },
              { 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), p.position.y },
              { 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), p.position.z }, { 0, 0, 0, 1 } } };
    return r;
}
static M rigid_inverse(M a)
{
    M r = ident();
    for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) r.m[i][j] = a.m[j][i];
    for (int i = 0; i < 3; i++) r.m[i][3] = -(r.m[i][0] * a.m[0][3] + r.m[i][1] * a.m[1][3] + r.m[i][2] * a.m[2][3]);
    return r;
}
static double pose_error(const float *f, M expect)
{
    XrPosef p = { { f[3], f[4], f[5], f[6] }, { f[0], f[1], f[2] } };
    M got = from_pose(p); double e = 0;
    for (int i = 0; i < 3; i++) for (int j = 0; j < 4; j++) e = fmax(e, fabs(got.m[i][j] - expect.m[i][j]));
    return e;
}

typedef MS void *(*Hook)(void *);
typedef MS int (*Sync)(float *, int *);
typedef MS int (*Locate)(float *, int *, int);
typedef MS int (*Vibrate)(int, float, float);
typedef MS int (*State)(void);
typedef MS const char *(*Report)(void);
typedef MS void (*SetViewScale)(float);
typedef MS void (*Views)(float *);

int main(int argc, char **argv)
{
    setvbuf(stdout, 0, _IONBF, 0);
    const char *scenario = argc > 2 ? argv[2] : "touch";
    if (!strcmp(scenario, "index")) current_profile = "/interaction_profiles/valve/index_controller";
    else if (!strcmp(scenario, "vive")) current_profile = "/interaction_profiles/htc/vive_controller";
    load(argc > 1 ? argv[1] : "../../GameFolder/XIII_Data/Plugins/xiii_openxr.dll");
    Hook hook = (Hook)export("XO_Hook"); Sync sync = (Sync)export("XO_Sync"); Locate locate = (Locate)export("XO_Locate");
    Vibrate vibrate = (Vibrate)export("XO_Vibrate"); State state = (State)export("XO_State"); Report report = (Report)export("XO_Report");
    CHECK(hook && sync && locate && vibrate && state && report, "exports missing");
    float f[64]; int i[8];
    CHECK(sync(f, i) == 0 && locate(f, i, 1) == 0, "read before any session");
    /* the plugin: the hooked proc-address function */
    PFN_xrGetInstanceProcAddr gipa = (PFN_xrGetInstanceProcAddr)hook((void *)f_gipa);
    CHECK(gipa && (void *)gipa != (void *)f_gipa, "no hook");
    PFN_xrVoidFunction fn;
    CHECK(gipa(0, "xrGetInstanceProcAddr", &fn) == XR_SUCCESS && (void *)fn == (void *)gipa, "the proc-address function asked for itself is not the hook");
    gipa(0, "xrCreateInstance", &fn); PFN_xrCreateInstance create_instance = (PFN_xrCreateInstance)fn;
    CHECK((void *)create_instance != (void *)f_CreateInstance, "xrCreateInstance not wrapped");
    XrInstance inst; XrInstanceCreateInfo ici = { XR_TYPE_INSTANCE_CREATE_INFO };
    CHECK(create_instance(&ici, &inst) == XR_SUCCESS && inst == INSTANCE, "instance");
    PFN_xrCreateSession create_session; gipa(inst, "xrCreateSession", (PFN_xrVoidFunction *)&create_session);
    PFN_xrBeginSession begin; gipa(inst, "xrBeginSession", (PFN_xrVoidFunction *)&begin);
    PFN_xrWaitFrame wait; gipa(inst, "xrWaitFrame", (PFN_xrVoidFunction *)&wait);
    PFN_xrPollEvent poll; gipa(inst, "xrPollEvent", (PFN_xrVoidFunction *)&poll);
    PFN_xrAttachSessionActionSets attach; gipa(inst, "xrAttachSessionActionSets", (PFN_xrVoidFunction *)&attach);
    PFN_xrDestroySession destroy_session; gipa(inst, "xrDestroySession", (PFN_xrVoidFunction *)&destroy_session);
    PFN_xrSyncActions plugin_sync; gipa(inst, "xrSyncActions", (PFN_xrVoidFunction *)&plugin_sync);
    PFN_xrLocateSpace plain; gipa(inst, "xrLocateSpace", (PFN_xrVoidFunction *)&plain);
    CHECK((void *)plain == (void *)f_LocateSpace, "a function the helper does not need was wrapped");
    XrSession s; XrSessionCreateInfo sci = { XR_TYPE_SESSION_CREATE_INFO }; sci.systemId = 7;
    CHECK(create_session(inst, &sci, &s) == XR_SUCCESS && s == SESSION, "session");
    CHECK(sets_made == 1 && actions_made == 10, "action set %d, actions %d", sets_made, actions_made);
    CHECK(profiles_suggested == 5, "bindings suggested for %d controller kinds (the HP one needs its extension: skipped here)", profiles_suggested);
    CHECK(has_binding("oculus/touch", "primary=/user/hand/right/input/a/click") && has_binding("oculus/touch", "primary=/user/hand/left/input/x/click")
          && has_binding("oculus/touch", "secondary=/user/hand/left/input/y/click") && has_binding("oculus/touch", "trigger=/user/hand/right/input/trigger/value")
          && has_binding("oculus/touch", "squeeze=/user/hand/left/input/squeeze/value") && has_binding("oculus/touch", "grip_pose=/user/hand/left/input/grip/pose")
          && has_binding("oculus/touch", "haptic=/user/hand/right/output/haptic") && has_binding("oculus/touch", "thumbstick=/user/hand/left/input/thumbstick")
          && !has_binding("oculus/touch", "primary=/user/hand/right/input/x/click"), "Oculus Touch bindings");
    CHECK(has_binding("valve/index", "primary=/user/hand/left/input/a/click") && has_binding("htc/vive", "thumbstick=/user/hand/right/input/trackpad")
          && has_binding("khr/simple", "trigger=/user/hand/right/input/select/click") && has_binding("microsoft/motion", "secondary=/user/hand/left/input/menu/click"), "other controllers' bindings");
    CHECK(sync(f, i) == 0, "buttons read before the actions were attached");
    CHECK(begin(s, 0) == XR_SUCCESS && attaches == 1 && attached_sets == 1, "the mod's actions not attached when the session began (%d)", attaches);
    CHECK(attach(s, 0) == XR_SUCCESS && attaches == 1, "the plugin's own attach after ours failed or attached twice");
    CHECK(locate(f, i, 1) == 0, "located before any frame");
    XrFrameState fs = { XR_TYPE_FRAME_STATE }; CHECK(wait(s, 0, &fs) == XR_SUCCESS, "wait");
    XrEventDataBuffer ev; while (poll(inst, &ev) == XR_SUCCESS) {}
    CHECK((state() & 15) == XR_SESSION_STATE_FOCUSED && (state() & 16), "state %d", state());
    /* buttons */
    CHECK(sync(f, i) == 1 && syncs == 1 && last_sync_count == 1, "sync");
    /* the plugin's own sync (no sets of its own): the mod's set goes along */
    XrActionsSyncInfo none = { XR_TYPE_ACTIONS_SYNC_INFO };
    CHECK(plugin_sync(s, &none) == XR_SUCCESS && syncs == 2 && last_sync_count == 1 && last_sync_set == (XrActionSet)0x3333, "the plugin's sync left the mod's set out (%d)", last_sync_count);
    XrActiveActionSet theirs[2] = { { (XrActionSet)0x9999, 0 }, { (XrActionSet)0x3333, 0 } }; XrActionsSyncInfo both = { XR_TYPE_ACTIONS_SYNC_INFO }; both.countActiveActionSets = 2; both.activeActionSets = theirs;
    CHECK(plugin_sync(s, &both) == XR_SUCCESS && last_sync_count == 2, "a sync already with the mod's set changed");
    CHECK(sync(f, i) == 1, "sync again");
    CHECK(fabsf(f[0] - .3f) < 1e-6 && fabsf(f[1] - .9f) < 1e-6 && fabsf(f[2] - .8f) < 1e-6 && fabsf(f[3] - .1f) < 1e-6, "trigger/squeeze %f %f %f %f", f[0], f[1], f[2], f[3]);
    CHECK(f[4] == -.25f && f[5] == 1 && f[6] == .5f && f[7] == 0, "sticks");
    CHECK(i[0] == 3 && i[1] == (2 | 8) && i[2] == 1, "active %d, left buttons %d, right %d", i[0], i[1], i[2]);
    int expect_profile = !strcmp(scenario, "index") ? 2 : !strcmp(scenario, "vive") ? 3 : 1;
    CHECK(i[3] == expect_profile && i[4] == expect_profile, "profiles %d %d", i[3], i[4]);
    /* poses: the next frame (one period ahead), in the floor space */
    CHECK(locate(f, i, 1) == 1, "locate");
    CHECK(located_at == 1000000000 + 11111111, "located at %lld", (long long)located_at);
    CHECK(fake_spaces[(uintptr_t)located_base - 0x5000].kind == 2, "not located in STAGE");
    CHECK((i[0] & 127) == 127, "flags %d", i[0]);
    CHECK(fabsf(f[0] - .1f) < 1e-6 && fabsf(f[1] - 1.7f) < 1e-6 && fabsf(f[6] - .9238795f) < 1e-6, "head");
    CHECK(fabsf(f[35] + .032f) < 1e-6 && fabsf(f[42] - .032f) < 1e-6 && f[49] == -.9f && f[50] == .8f && f[51] == .85f && f[52] == -.95f, "eyes / fov");
    CHECK(fabsf(f[57] - 90) < .01f, "headset Hz %f", f[57]);
    for (int h = 0; h < 2; h++) {
        M g = ident();
        if (expect_profile == 1) g = mul(trans(h ? -0.007 : 0.007, -0.00182941, 0.1019482), rot(0, 20.6));
        if (expect_profile == 2) g = mul(mul(mul(trans(0, -0.015, 0.13), rot(0, 15.392)), rot(1, h ? 2.071 : -2.071)), rot(2, h ? -0.303 : 0.303));
        M want = mul(from_pose(grip_pose[h]), rigid_inverse(g));
        double e = pose_error(f + 7 + 7 * h, want);
        CHECK(e < 2e-5, "%s hand raw pose off by %g", h ? "right" : "left", e);
    }
    /* vibration */
    CHECK(vibrate(1, .5f, .055f) == 1 && haptic_duration == 55000000 && fabsf(haptic_amplitude - .5f) < 1e-6 && right(haptic_hand), "vibration right");
    CHECK(vibrate(0, 2, 0) == 1 && haptic_duration == XR_MIN_HAPTIC_DURATION && haptic_amplitude == 1 && !right(haptic_hand), "vibration left, clamped, shortest");
    CHECK(vibrate(0, 0, .1f) == 0 && vibrate(2, 1, .1f) == 0 && haptics == 2, "a silent or unknown vibration sent");
    /* 0.1.235: the world scale - the plugin's views moved apart about their middle, the mod's own not */
    {
        SetViewScale set_scale = (SetViewScale)export("XO_SetViewScale"); Views views_of = (Views)export("XO_Views");
        CHECK(set_scale && views_of, "world scale exports missing");
        PFN_xrLocateViews plugin_views; gipa(inst, "xrLocateViews", (PFN_xrVoidFunction *)&plugin_views);
        CHECK((void *)plugin_views != (void *)f_LocateViews, "xrLocateViews not wrapped");
        XrViewLocateInfo vli = { XR_TYPE_VIEW_LOCATE_INFO }; XrViewState vst = { XR_TYPE_VIEW_STATE };
        XrView v[2] = { { XR_TYPE_VIEW }, { XR_TYPE_VIEW } }; uint32_t vn = 0; float w[4];
        CHECK(plugin_views(s, &vli, &vst, 2, &vn, v) == XR_SUCCESS && vn == 2 && fabsf(v[0].pose.position.x + .032f) < 1e-6 && fabsf(v[1].pose.position.x - .032f) < 1e-6, "views changed at scale 1");
        set_scale(1.25f);
        CHECK(plugin_views(s, &vli, &vst, 2, &vn, v) == XR_SUCCESS && fabsf(v[0].pose.position.x + .04f) < 1e-6 && fabsf(v[1].pose.position.x - .04f) < 1e-6, "views not moved apart: %f %f", v[0].pose.position.x, v[1].pose.position.x);
        CHECK(v[0].fov.angleLeft == -.9f && v[1].fov.angleRight == .9f && v[0].pose.orientation.w == 1, "the views' field of view or turn changed");
        views_of(w); CHECK(fabsf(w[0] - .064f * .064f) < 1e-7 && fabsf(w[1] - .08f * .08f) < 1e-7 && w[2] == 2 && w[3] == 1, "views report %f %f %f %f", w[0], w[1], w[2], w[3]);
        CHECK(locate(f, i, 1) == 1 && fabsf(f[35] + .032f) < 1e-6 && fabsf(f[42] - .032f) < 1e-6, "the mod's own eyes scaled too");
        CHECK(plugin_views(s, &vli, &vst, 0, &vn, 0) == XR_SUCCESS, "a count query broke");
        set_scale(9); CHECK(plugin_views(s, &vli, &vst, 2, &vn, v) == XR_SUCCESS && fabsf(v[1].pose.position.x - .032f) < 1e-6, "an out-of-range scale used");
        set_scale(.8f); CHECK(plugin_views(s, &vli, &vst, 2, &vn, v) == XR_SUCCESS && fabsf(v[1].pose.position.x - .0256f) < 1e-6, "views not moved together");
        set_scale(1);
    }
    printf("report: %s\n", report());
    CHECK(strstr(report(), "Fake Pimax OpenXR 1.2.3") && strstr(report(), "Pimax Crystal") && strstr(report(), "STAGE") && strstr(report(), "HP Reverb G2 -"), "report");
    /* the session ends: nothing more is read or sent */
    CHECK(destroy_session(s) == XR_SUCCESS && state() == 0 && sync(f, i) == 0 && locate(f, i, 1) == 0 && vibrate(1, 1, .1f) == 0, "after the session");
    if (failures) printf("FAILED: %d (%s)\n", failures, scenario); else printf("ALL OK (%s)\n", scenario);
    return failures != 0;
}
