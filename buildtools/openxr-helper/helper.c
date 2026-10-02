/* xiii_openxr.dll: the mod's own OpenXR input and tracking (0.1.181).
 *
 * The game renders into the headset through Unity's OpenXR plugin
 * (UnityOpenXR.dll, started by the mod like Unity's own OpenXR loader). That
 * plugin lets a feature wrap xrGetInstanceProcAddr (NativeConfig_
 * SetProcAddressPtrAndLoadStage1); XO_Hook gives it this DLL's wrapper, which
 * sees the plugin's calls and adds, beside them:
 *   - the instance and session (xrCreateInstance / xrCreateSession);
 *   - the display time of the frames (xrWaitFrame: predictedDisplayTime);
 *   - the mod's own action set (buttons, trigger, grip, sticks, hand poses,
 *     vibration) with bindings for the usual controllers, attached to the
 *     session when it begins;
 *   - interaction profile changes (xrPollEvent);
 *   - the plugin's own xrSyncActions keeps the mod's set in (else it would
 *     go inactive between the mod's reads).
 * The mod reads it once a frame: XO_Sync (buttons) and XO_Locate (head, eyes,
 * hands, field of view, all at the next frame's display time, in the STAGE
 * (floor) space, else LOCAL), and vibrates with XO_Vibrate.
 *
 * Hand poses are given like SteamVR's (the mod was tuned on those): the
 * OpenXR grip pose times OpenComposite's grip-to-SteamVR transform of that
 * controller (Oculus Touch, Index, HP Reverb G2; others as they are).
 *
 * No C runtime, no Windows SDK, no imports: every OpenXR call goes through
 * the function pointers the loader gives. Built with clang + lld-link
 * (build.sh); tested on Linux against a fake runtime (helpertest.c).
 */
#define XR_NO_PROTOTYPES
#include "openxr/openxr.h"

typedef unsigned long long u64;
typedef long long i64;

int _fltused = 0;
/* the compiler clears and copies the big OpenXR structs with these (volatile: never turned back into calls) */
void *memset(void *d, int c, unsigned long long n) { volatile unsigned char *p = (volatile unsigned char *)d; while (n--) *p++ = (unsigned char)c; return d; }
void *memcpy(void *d, const void *s, unsigned long long n) { volatile unsigned char *p = (volatile unsigned char *)d; const volatile unsigned char *q = (const volatile unsigned char *)s; while (n--) *p++ = *q++; return d; }

/* ---- small helpers (no C runtime) ---- */
static int same(const char *a, const char *b) { while (*a && *a == *b) { a++; b++; } return *a == *b; }
static void copy(char *out, int size, const char *s) { int n = 0; while (s && s[n] && n < size - 1) { out[n] = s[n]; n++; } out[n] = 0; }
static char report[2048];
static int report_n;
static void note(const char *s) { while (s && *s && report_n < (int)sizeof(report) - 1) report[report_n++] = *s++; report[report_n] = 0; }
static void note_int(i64 v)
{
    char b[24]; int n = 0; int neg = v < 0; u64 u = neg ? (u64)(-v) : (u64)v;
    do { b[n++] = (char)('0' + u % 10); u /= 10; } while (u && n < 22);
    if (neg) b[n++] = '-';
    char o[24]; for (int i = 0; i < n; i++) o[i] = b[n - 1 - i]; o[n] = 0; note(o);
}

/* ---- the loader's functions (next in the chain) ---- */
static PFN_xrGetInstanceProcAddr gipa_next;
static struct {
    PFN_xrCreateInstance CreateInstance; PFN_xrDestroyInstance DestroyInstance;
    PFN_xrCreateSession CreateSession; PFN_xrDestroySession DestroySession;
    PFN_xrBeginSession BeginSession; PFN_xrWaitFrame WaitFrame; PFN_xrPollEvent PollEvent;
    PFN_xrAttachSessionActionSets Attach; PFN_xrSyncActions SyncActions;
} next;
static struct {
    PFN_xrStringToPath StringToPath; PFN_xrPathToString PathToString;
    PFN_xrCreateActionSet CreateActionSet; PFN_xrDestroyActionSet DestroyActionSet; PFN_xrCreateAction CreateAction;
    PFN_xrSuggestInteractionProfileBindings Suggest; PFN_xrAttachSessionActionSets Attach;
    PFN_xrCreateActionSpace CreateActionSpace; PFN_xrCreateReferenceSpace CreateReferenceSpace;
    PFN_xrEnumerateReferenceSpaces EnumerateReferenceSpaces; PFN_xrDestroySpace DestroySpace;
    PFN_xrSyncActions Sync; PFN_xrGetActionStateFloat GetFloat; PFN_xrGetActionStateBoolean GetBool;
    PFN_xrGetActionStateVector2f GetVec2; PFN_xrLocateSpace LocateSpace; PFN_xrLocateViews LocateViews;
    PFN_xrApplyHapticFeedback Haptic; PFN_xrStopHapticFeedback StopHaptic;
    PFN_xrGetCurrentInteractionProfile CurrentProfile;
    PFN_xrGetInstanceProperties InstanceProperties; PFN_xrGetSystemProperties SystemProperties;
} xr;
static int resolve(XrInstance inst, const char *name, PFN_xrVoidFunction *out)
{
    *out = 0;
    return gipa_next && gipa_next(inst, name, out) == XR_SUCCESS && *out;
}

/* ---- state ---- */
static XrInstance instance;
static XrSession session;
static XrSystemId system_id;
static volatile i64 predicted_time, predicted_period;
static volatile int session_state;
static int attached, actions_ready, profiles_dirty, stage;
static XrActionSet set;
enum { A_GRIP, A_AIM, A_TRIGGER, A_SQUEEZE, A_PRIMARY, A_SECONDARY, A_MENU, A_STICK, A_STICK_CLICK, A_HAPTIC, A_COUNT };
static XrAction actions[A_COUNT];
static XrPath hand_path[2];
static XrSpace grip_space[2], aim_space[2], view_space, base_space;
enum { P_NONE, P_TOUCH, P_INDEX, P_VIVE, P_WMR, P_HP, P_SIMPLE, P_OTHER };
static int profile[2];

/* ---- the mod's action set ---- */
static const struct { const char *name, *label; XrActionType type; } action_defs[A_COUNT] = {
    { "grip_pose", "Hand", XR_ACTION_TYPE_POSE_INPUT },
    { "aim_pose", "Aim", XR_ACTION_TYPE_POSE_INPUT },
    { "trigger", "Trigger (fire)", XR_ACTION_TYPE_FLOAT_INPUT },
    { "squeeze", "Grip (take hold)", XR_ACTION_TYPE_FLOAT_INPUT },
    { "primary", "A / X", XR_ACTION_TYPE_BOOLEAN_INPUT },
    { "secondary", "B / Y", XR_ACTION_TYPE_BOOLEAN_INPUT },
    { "menu", "Menu", XR_ACTION_TYPE_BOOLEAN_INPUT },
    { "thumbstick", "Stick", XR_ACTION_TYPE_VECTOR2F_INPUT },
    { "thumbstick_click", "Stick click", XR_ACTION_TYPE_BOOLEAN_INPUT },
    { "haptic", "Vibration", XR_ACTION_TYPE_VIBRATION_OUTPUT },
};
/* hands: 1 left, 2 right, 3 both */
typedef struct { int action, hands; const char *path; } Binding;
#define POSES { A_GRIP, 3, "input/grip/pose" }, { A_AIM, 3, "input/aim/pose" }, { A_HAPTIC, 3, "output/haptic" }
static const Binding touch[] = { POSES, { A_TRIGGER, 3, "input/trigger/value" }, { A_SQUEEZE, 3, "input/squeeze/value" },
    { A_PRIMARY, 2, "input/a/click" }, { A_SECONDARY, 2, "input/b/click" }, { A_PRIMARY, 1, "input/x/click" }, { A_SECONDARY, 1, "input/y/click" },
    { A_MENU, 1, "input/menu/click" }, { A_STICK, 3, "input/thumbstick" }, { A_STICK_CLICK, 3, "input/thumbstick/click" }, { -1 } };
static const Binding index_[] = { POSES, { A_TRIGGER, 3, "input/trigger/value" }, { A_SQUEEZE, 3, "input/squeeze/value" },
    { A_PRIMARY, 3, "input/a/click" }, { A_SECONDARY, 3, "input/b/click" }, { A_STICK, 3, "input/thumbstick" }, { A_STICK_CLICK, 3, "input/thumbstick/click" }, { -1 } };
static const Binding vive[] = { POSES, { A_TRIGGER, 3, "input/trigger/value" }, { A_SQUEEZE, 3, "input/squeeze/click" },
    { A_SECONDARY, 3, "input/menu/click" }, { A_STICK, 3, "input/trackpad" }, { A_STICK_CLICK, 3, "input/trackpad/click" }, { -1 } };
static const Binding wmr[] = { POSES, { A_TRIGGER, 3, "input/trigger/value" }, { A_SQUEEZE, 3, "input/squeeze/click" },
    { A_PRIMARY, 3, "input/trackpad/click" }, { A_SECONDARY, 3, "input/menu/click" }, { A_STICK, 3, "input/thumbstick" }, { A_STICK_CLICK, 3, "input/thumbstick/click" }, { -1 } };
static const Binding hp[] = { POSES, { A_TRIGGER, 3, "input/trigger/value" }, { A_SQUEEZE, 3, "input/squeeze/value" },
    { A_PRIMARY, 2, "input/a/click" }, { A_SECONDARY, 2, "input/b/click" }, { A_PRIMARY, 1, "input/x/click" }, { A_SECONDARY, 1, "input/y/click" },
    { A_MENU, 3, "input/menu/click" }, { A_STICK, 3, "input/thumbstick" }, { A_STICK_CLICK, 3, "input/thumbstick/click" }, { -1 } };
static const Binding simple[] = { POSES, { A_TRIGGER, 3, "input/select/click" }, { A_SECONDARY, 3, "input/menu/click" }, { -1 } };
static const struct { const char *path, *name; int id; const Binding *bindings; } profiles[] = {
    { "/interaction_profiles/oculus/touch_controller", "Oculus Touch", P_TOUCH, touch },
    { "/interaction_profiles/valve/index_controller", "Index", P_INDEX, index_ },
    { "/interaction_profiles/hp/mixed_reality_controller", "HP Reverb G2", P_HP, hp },
    { "/interaction_profiles/microsoft/motion_controller", "WMR", P_WMR, wmr },
    { "/interaction_profiles/htc/vive_controller", "Vive", P_VIVE, vive },
    { "/interaction_profiles/khr/simple_controller", "simple", P_SIMPLE, simple },
};
#define PROFILES ((int)(sizeof(profiles) / sizeof(profiles[0])))

static XrPath path(const char *s)
{
    XrPath p = XR_NULL_PATH;
    if (xr.StringToPath && xr.StringToPath(instance, s, &p) != XR_SUCCESS) p = XR_NULL_PATH;
    return p;
}
static void fail(const char *what, XrResult r) { note(" "); note(what); note(" failed ("); note_int(r); note(");"); }

static void destroy_actions(void)
{
    for (int h = 0; h < 2; h++) {
        if (grip_space[h] && xr.DestroySpace) xr.DestroySpace(grip_space[h]);
        if (aim_space[h] && xr.DestroySpace) xr.DestroySpace(aim_space[h]);
        grip_space[h] = aim_space[h] = XR_NULL_HANDLE;
    }
    if (view_space && xr.DestroySpace) xr.DestroySpace(view_space);
    if (base_space && xr.DestroySpace) xr.DestroySpace(base_space);
    view_space = base_space = XR_NULL_HANDLE;
    if (set && xr.DestroyActionSet) xr.DestroyActionSet(set); /* its actions go with it */
    set = XR_NULL_HANDLE;
    for (int a = 0; a < A_COUNT; a++) actions[a] = XR_NULL_HANDLE;
    actions_ready = attached = 0;
    profile[0] = profile[1] = P_NONE;
}

static int make_actions(void)
{
    if (!instance || !xr.CreateActionSet || !xr.CreateAction || !xr.StringToPath) { note(" instance functions missing;"); return 0; }
    hand_path[0] = path("/user/hand/left"); hand_path[1] = path("/user/hand/right");
    XrActionSetCreateInfo si = { XR_TYPE_ACTION_SET_CREATE_INFO };
    copy(si.actionSetName, sizeof(si.actionSetName), "xiii_vr");
    copy(si.localizedActionSetName, sizeof(si.localizedActionSetName), "XIII VR");
    si.priority = 0;
    XrResult r = xr.CreateActionSet(instance, &si, &set);
    if (r != XR_SUCCESS) { fail("xrCreateActionSet", r); return 0; }
    for (int a = 0; a < A_COUNT; a++) {
        XrActionCreateInfo ai = { XR_TYPE_ACTION_CREATE_INFO };
        copy(ai.actionName, sizeof(ai.actionName), action_defs[a].name);
        copy(ai.localizedActionName, sizeof(ai.localizedActionName), action_defs[a].label);
        ai.actionType = action_defs[a].type;
        ai.countSubactionPaths = 2; ai.subactionPaths = hand_path;
        r = xr.CreateAction(set, &ai, &actions[a]);
        if (r != XR_SUCCESS) { fail(action_defs[a].name, r); return 0; }
    }
    /* suggested bindings, each controller kind on its own (an unknown one is skipped) */
    note(" bindings:");
    for (int p = 0; p < PROFILES; p++) {
        XrActionSuggestedBinding b[40]; int n = 0;
        for (const Binding *d = profiles[p].bindings; d->action >= 0 && n < 38; d++)
            for (int h = 0; h < 2; h++) {
                if (!(d->hands & (1 << h))) continue;
                char full[160]; int k = 0;
                const char *prefix = h ? "/user/hand/right/" : "/user/hand/left/";
                for (const char *s = prefix; *s; s++) full[k++] = *s;
                for (const char *s = d->path; *s && k < 158; s++) full[k++] = *s;
                full[k] = 0;
                XrPath bp = path(full);
                if (bp == XR_NULL_PATH) continue;
                b[n].action = actions[d->action]; b[n].binding = bp; n++;
            }
        XrInteractionProfileSuggestedBinding sb = { XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING };
        sb.interactionProfile = path(profiles[p].path);
        sb.countSuggestedBindings = (uint32_t)n; sb.suggestedBindings = b;
        r = sb.interactionProfile ? xr.Suggest(instance, &sb) : XR_ERROR_PATH_UNSUPPORTED;
        note(" "); note(profiles[p].name); note(r == XR_SUCCESS ? " ok" : " -");
    }
    note(";");
    return 1;
}

static void make_spaces(void)
{
    XrPosef identity = { { 0, 0, 0, 1 }, { 0, 0, 0 } };
    for (int h = 0; h < 2; h++) {
        XrActionSpaceCreateInfo ci = { XR_TYPE_ACTION_SPACE_CREATE_INFO };
        ci.subactionPath = hand_path[h]; ci.poseInActionSpace = identity;
        ci.action = actions[A_GRIP]; if (xr.CreateActionSpace(session, &ci, &grip_space[h]) != XR_SUCCESS) grip_space[h] = XR_NULL_HANDLE;
        ci.action = actions[A_AIM]; if (xr.CreateActionSpace(session, &ci, &aim_space[h]) != XR_SUCCESS) aim_space[h] = XR_NULL_HANDLE;
    }
    XrReferenceSpaceCreateInfo ri = { XR_TYPE_REFERENCE_SPACE_CREATE_INFO };
    ri.poseInReferenceSpace = identity;
    ri.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_VIEW;
    if (xr.CreateReferenceSpace(session, &ri, &view_space) != XR_SUCCESS) view_space = XR_NULL_HANDLE;
    /* STAGE (the floor, like SteamVR's standing space) when the runtime has it */
    XrReferenceSpaceType types[16]; uint32_t count = 0;
    stage = 0;
    if (xr.EnumerateReferenceSpaces && xr.EnumerateReferenceSpaces(session, 16, &count, types) == XR_SUCCESS)
        for (uint32_t i = 0; i < count && i < 16; i++) if (types[i] == XR_REFERENCE_SPACE_TYPE_STAGE) stage = 1;
    ri.referenceSpaceType = stage ? XR_REFERENCE_SPACE_TYPE_STAGE : XR_REFERENCE_SPACE_TYPE_LOCAL;
    if (xr.CreateReferenceSpace(session, &ri, &base_space) != XR_SUCCESS) {
        base_space = XR_NULL_HANDLE;
        if (stage) { stage = 0; ri.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_LOCAL; if (xr.CreateReferenceSpace(session, &ri, &base_space) != XR_SUCCESS) base_space = XR_NULL_HANDLE; }
    }
    note(stage ? " space STAGE (floor);" : " space LOCAL (no STAGE);");
}

/* Attach the mod's set (and whatever the plugin attaches with it): once per session. */
static XrResult attach_with(const XrSessionActionSetsAttachInfo *theirs)
{
    XrActionSet sets[16]; uint32_t n = 0;
    if (theirs) for (uint32_t i = 0; i < theirs->countActionSets && n < 15; i++) sets[n++] = theirs->actionSets[i];
    if (set) sets[n++] = set;
    if (!n) return XR_SUCCESS;
    XrSessionActionSetsAttachInfo info = { XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO };
    info.countActionSets = n; info.actionSets = sets;
    XrResult r = (next.Attach ? next.Attach : xr.Attach)(session, &info);
    if (r == XR_SUCCESS) { attached = 1; note(" actions attached;"); }
    else fail("xrAttachSessionActionSets", r);
    return r;
}

/* ---- the wrappers ---- */
static XRAPI_ATTR XrResult XRAPI_CALL hook_create_instance(const XrInstanceCreateInfo *info, XrInstance *out)
{
    XrResult r = next.CreateInstance(info, out);
    if (r != XR_SUCCESS || !out) return r;
    instance = *out;
#define R(field, name) resolve(instance, name, (PFN_xrVoidFunction *)&xr.field)
    R(StringToPath, "xrStringToPath"); R(PathToString, "xrPathToString");
    R(CreateActionSet, "xrCreateActionSet"); R(DestroyActionSet, "xrDestroyActionSet"); R(CreateAction, "xrCreateAction");
    R(Suggest, "xrSuggestInteractionProfileBindings"); R(Attach, "xrAttachSessionActionSets");
    R(CreateActionSpace, "xrCreateActionSpace"); R(CreateReferenceSpace, "xrCreateReferenceSpace");
    R(EnumerateReferenceSpaces, "xrEnumerateReferenceSpaces"); R(DestroySpace, "xrDestroySpace");
    R(Sync, "xrSyncActions"); R(GetFloat, "xrGetActionStateFloat"); R(GetBool, "xrGetActionStateBoolean");
    R(GetVec2, "xrGetActionStateVector2f"); R(LocateSpace, "xrLocateSpace"); R(LocateViews, "xrLocateViews");
    R(Haptic, "xrApplyHapticFeedback"); R(StopHaptic, "xrStopHapticFeedback"); R(CurrentProfile, "xrGetCurrentInteractionProfile");
    R(InstanceProperties, "xrGetInstanceProperties"); R(SystemProperties, "xrGetSystemProperties");
#undef R
    XrInstanceProperties props = { XR_TYPE_INSTANCE_PROPERTIES };
    if (xr.InstanceProperties && xr.InstanceProperties(instance, &props) == XR_SUCCESS) {
        note(" runtime "); note(props.runtimeName); note(" ");
        note_int(XR_VERSION_MAJOR(props.runtimeVersion)); note("."); note_int(XR_VERSION_MINOR(props.runtimeVersion)); note("."); note_int(XR_VERSION_PATCH(props.runtimeVersion)); note(";");
    }
    return r;
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_destroy_instance(XrInstance inst)
{
    if (inst == instance) { destroy_actions(); session = XR_NULL_HANDLE; instance = XR_NULL_HANDLE; predicted_time = 0; }
    return next.DestroyInstance(inst);
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_create_session(XrInstance inst, const XrSessionCreateInfo *info, XrSession *out)
{
    XrResult r = next.CreateSession(inst, info, out);
    if (r != XR_SUCCESS || !out) return r;
    session = *out; system_id = info ? info->systemId : 0; predicted_time = 0; session_state = 0;
    XrSystemProperties sp = { XR_TYPE_SYSTEM_PROPERTIES };
    if (xr.SystemProperties && xr.SystemProperties(instance, system_id, &sp) == XR_SUCCESS) { note(" system "); note(sp.systemName); note(";"); }
    destroy_actions();
    if (make_actions()) { make_spaces(); actions_ready = 1; profiles_dirty = 1; }
    return r;
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_destroy_session(XrSession s)
{
    if (s == session) { destroy_actions(); session = XR_NULL_HANDLE; predicted_time = 0; session_state = 0; }
    return next.DestroySession(s);
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_begin_session(XrSession s, const XrSessionBeginInfo *info)
{
    XrResult r = next.BeginSession(s, info);
    if (r == XR_SUCCESS && s == session && actions_ready && !attached) attach_with(0);
    return r;
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_attach(XrSession s, const XrSessionActionSetsAttachInfo *info)
{
    if (s != session || !actions_ready) return next.Attach(s, info);
    if (attached) return XR_SUCCESS; /* ours went on at the session's start; the plugin's own were never made */
    return attach_with(info);
}
/* The plugin's own sync keeps the mod's set active too (a set left out of a sync goes inactive). */
static XRAPI_ATTR XrResult XRAPI_CALL hook_sync(XrSession s, const XrActionsSyncInfo *info)
{
    if (s != session || !attached || !set || !info) return next.SyncActions(s, info);
    for (uint32_t k = 0; k < info->countActiveActionSets; k++) if (info->activeActionSets[k].actionSet == set) return next.SyncActions(s, info);
    XrActiveActionSet sets[16]; uint32_t n = 0;
    for (uint32_t k = 0; k < info->countActiveActionSets && n < 15; k++) sets[n++] = info->activeActionSets[k];
    sets[n].actionSet = set; sets[n].subactionPath = XR_NULL_PATH; n++;
    XrActionsSyncInfo merged = *info;
    merged.countActiveActionSets = n; merged.activeActionSets = sets;
    return next.SyncActions(s, &merged);
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_wait_frame(XrSession s, const XrFrameWaitInfo *info, XrFrameState *state)
{
    XrResult r = next.WaitFrame(s, info, state);
    if ((r == XR_SUCCESS || r == XR_SESSION_LOSS_PENDING) && state && s == session) {
        predicted_period = state->predictedDisplayPeriod;
        predicted_time = state->predictedDisplayTime;
    }
    return r;
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_poll_event(XrInstance inst, XrEventDataBuffer *event)
{
    XrResult r = next.PollEvent(inst, event);
    if (r == XR_SUCCESS && event) {
        if (event->type == XR_TYPE_EVENT_DATA_INTERACTION_PROFILE_CHANGED) profiles_dirty = 1;
        else if (event->type == XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED) {
            const XrEventDataSessionStateChanged *c = (const XrEventDataSessionStateChanged *)event;
            if (c->session == session) session_state = (int)c->state;
        }
    }
    return r;
}
static XRAPI_ATTR XrResult XRAPI_CALL hook_gipa(XrInstance inst, const char *name, PFN_xrVoidFunction *fn)
{
    XrResult r = gipa_next(inst, name, fn);
    if (r != XR_SUCCESS || !fn || !*fn || !name) return r;
#define H(field, hook) if (same(name, "xr" #field)) { next.field = (PFN_xr##field)*fn; *fn = (PFN_xrVoidFunction)hook; return r; }
    H(CreateInstance, hook_create_instance) H(DestroyInstance, hook_destroy_instance)
    H(CreateSession, hook_create_session) H(DestroySession, hook_destroy_session)
    H(BeginSession, hook_begin_session) H(WaitFrame, hook_wait_frame) H(PollEvent, hook_poll_event)
    if (same(name, "xrSyncActions")) { next.SyncActions = (PFN_xrSyncActions)*fn; *fn = (PFN_xrVoidFunction)hook_sync; return r; }
#undef H
    /* the plugin asking for the proc-address function itself keeps getting this one */
    if (same(name, "xrGetInstanceProcAddr")) { *fn = (PFN_xrVoidFunction)hook_gipa; return r; }
    if (same(name, "xrAttachSessionActionSets")) { next.Attach = (PFN_xrAttachSessionActionSets)*fn; *fn = (PFN_xrVoidFunction)hook_attach; }
    return r;
}

/* ---- for the mod ---- */
__declspec(dllexport) void *XO_Hook(void *loader_gipa)
{
    if (!loader_gipa) return 0;
    if (loader_gipa != (void *)hook_gipa) gipa_next = (PFN_xrGetInstanceProcAddr)loader_gipa;
    if (!report_n) note("XIII VR OpenXR 0.1.181:");
    return (void *)hook_gipa;
}
__declspec(dllexport) const char *XO_Report(void) { return report; }
/* 0 no session, else the session state (XrSessionState: 1 idle .. 5 focused ..); +16 when the mod's actions are attached */
__declspec(dllexport) int XO_State(void) { return session ? session_state + (attached ? 16 : 0) : 0; }

static int profile_of(XrPath p)
{
    if (!p || !xr.PathToString) return P_NONE;
    char s[XR_MAX_PATH_LENGTH]; uint32_t n = 0;
    if (xr.PathToString(instance, p, sizeof(s), &n, s) != XR_SUCCESS) return P_OTHER;
    for (int i = 0; i < PROFILES; i++) if (same(s, profiles[i].path)) return profiles[i].id;
    note(" other controller "); note(s); note(";");
    return P_OTHER;
}
static void read_profiles(void)
{
    profiles_dirty = 0;
    if (!xr.CurrentProfile) return;
    for (int h = 0; h < 2; h++) {
        XrInteractionProfileState st = { XR_TYPE_INTERACTION_PROFILE_STATE };
        int before = profile[h];
        profile[h] = xr.CurrentProfile(session, hand_path[h], &st) == XR_SUCCESS ? profile_of(st.interactionProfile) : P_NONE;
        if (profile[h] != before && profile[h] != P_NONE) {
            note(h ? " right: " : " left: ");
            for (int i = 0; i < PROFILES; i++) if (profiles[i].id == profile[h]) note(profiles[i].name);
            if (profile[h] == P_OTHER) note("other");
            note(";");
        }
    }
}

/* Buttons. f: trigger L,R; squeeze L,R; stick Lx,Ly,Rx,Ry (8). i: [0] active bits (1 left, 2 right),
 * [1] left buttons, [2] right buttons (1 A/X, 2 B/Y, 4 menu, 8 stick click), [3] left profile, [4] right profile.
 * Returns 1 read, 0 not yet (no attached actions or not focused), <0 an OpenXR error. */
__declspec(dllexport) int XO_Sync(float *f, int *i)
{
    for (int k = 0; k < 8; k++) f[k] = 0;
    for (int k = 0; k < 5; k++) i[k] = 0;
    if (!session || !attached || !xr.Sync) return 0;
    XrActiveActionSet active = { set, XR_NULL_PATH };
    XrActionsSyncInfo si = { XR_TYPE_ACTIONS_SYNC_INFO };
    si.countActiveActionSets = 1; si.activeActionSets = &active;
    XrResult r = xr.Sync(session, &si);
    if (r == XR_SESSION_NOT_FOCUSED) return 0;
    if (r != XR_SUCCESS) return (int)r;
    if (profiles_dirty) read_profiles();
    for (int h = 0; h < 2; h++) {
        XrActionStateGetInfo gi = { XR_TYPE_ACTION_STATE_GET_INFO };
        gi.subactionPath = hand_path[h];
        XrActionStateFloat fs = { XR_TYPE_ACTION_STATE_FLOAT };
        int on = 0;
        gi.action = actions[A_TRIGGER]; if (xr.GetFloat(session, &gi, &fs) == XR_SUCCESS && fs.isActive) { f[h] = fs.currentState; on = 1; }
        gi.action = actions[A_SQUEEZE]; fs.isActive = 0; if (xr.GetFloat(session, &gi, &fs) == XR_SUCCESS && fs.isActive) { f[2 + h] = fs.currentState; on = 1; }
        XrActionStateVector2f vs = { XR_TYPE_ACTION_STATE_VECTOR2F };
        gi.action = actions[A_STICK]; if (xr.GetVec2(session, &gi, &vs) == XR_SUCCESS && vs.isActive) { f[4 + 2 * h] = vs.currentState.x; f[5 + 2 * h] = vs.currentState.y; on = 1; }
        static const int bool_actions[4] = { A_PRIMARY, A_SECONDARY, A_MENU, A_STICK_CLICK };
        for (int b = 0; b < 4; b++) {
            XrActionStateBoolean bs = { XR_TYPE_ACTION_STATE_BOOLEAN };
            gi.action = actions[bool_actions[b]];
            if (xr.GetBool(session, &gi, &bs) == XR_SUCCESS && bs.isActive) { on = 1; if (bs.currentState) i[1 + h] |= 1 << b; }
        }
        if (on) i[0] |= 1 << h;
        i[3 + h] = profile[h];
    }
    return 1;
}

/* ---- poses ---- */
typedef struct { float x, y, z, w; } Q;
static Q qmul(Q a, Q b)
{
    Q r = { a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z };
    return r;
}
static XrVector3f qrot(Q q, XrVector3f v)
{
    Q p = { v.x, v.y, v.z, 0 }, c = { -q.x, -q.y, -q.z, q.w };
    Q r = qmul(qmul(q, p), c);
    XrVector3f o = { r.x, r.y, r.z }; return o;
}
/* sin/cos without the C runtime (angles of a few tens of degrees) */
static float sine(float x) { float x2 = x * x; return x * (1 - x2 / 6 * (1 - x2 / 20 * (1 - x2 / 42 * (1 - x2 / 72 * (1 - x2 / 110))))); }
static float cosine(float x) { float x2 = x * x; return 1 - x2 / 2 * (1 - x2 / 12 * (1 - x2 / 30 * (1 - x2 / 56 * (1 - x2 / 90)))); }
static Q axis_angle(int axis, float degrees)
{
    float h = degrees * 3.14159265f / 360.0f; Q q = { 0, 0, 0, cosine(h) };
    if (axis == 0) q.x = sine(h); else if (axis == 1) q.y = sine(h); else q.z = sine(h);
    return q;
}
/* OpenComposite's grip-to-SteamVR transforms (inverse of the openxr_grip component of SteamVR's
 * render model: translate o, rotate x, y, z degrees). */
static int grip_transform(int prof, int right, XrPosef *out)
{
    float o[3], a[3];
    if (prof == P_TOUCH) { o[0] = right ? -0.007f : 0.007f; o[1] = -0.00182941f; o[2] = 0.1019482f; a[0] = 20.6f; a[1] = 0; a[2] = 0; }
    else if (prof == P_INDEX) { o[0] = 0; o[1] = -0.015f; o[2] = 0.13f; a[0] = 15.392f; a[1] = right ? 2.071f : -2.071f; a[2] = right ? -0.303f : 0.303f; }
    else if (prof == P_HP) { o[0] = 0; o[1] = -0.00553f; o[2] = 0.09689f; a[0] = -5.036f; a[1] = 0; a[2] = 0; }
    else return 0;
    Q g = qmul(qmul(axis_angle(0, a[0]), axis_angle(1, a[1])), axis_angle(2, a[2]));
    Q inv = { -g.x, -g.y, -g.z, g.w };
    XrVector3f t = { o[0], o[1], o[2] };
    XrVector3f it = qrot(inv, t);
    out->orientation.x = inv.x; out->orientation.y = inv.y; out->orientation.z = inv.z; out->orientation.w = inv.w;
    out->position.x = -it.x; out->position.y = -it.y; out->position.z = -it.z;
    return 1;
}
static void put(float *f, const XrPosef *p)
{
    f[0] = p->position.x; f[1] = p->position.y; f[2] = p->position.z;
    f[3] = p->orientation.x; f[4] = p->orientation.y; f[5] = p->orientation.z; f[6] = p->orientation.w;
}
static int locate(XrSpace space, XrTime t, XrPosef *out)
{
    if (!space || !base_space) return 0;
    XrSpaceLocation l = { XR_TYPE_SPACE_LOCATION };
    if (xr.LocateSpace(space, base_space, t, &l) != XR_SUCCESS) return 0;
    const XrSpaceLocationFlags need = XR_SPACE_LOCATION_POSITION_VALID_BIT | XR_SPACE_LOCATION_ORIENTATION_VALID_BIT;
    if ((l.locationFlags & need) != need) return 0;
    *out = l.pose; return 1;
}
/* f (58): head 0-6; left hand 7-13; right hand 14-20; left aim 21-27; right aim 28-34 (x y z qx qy qz qw,
 * OpenXR space: right-handed, -Z forward); eye left 35-41, eye right 42-48 (from the head);
 * fov left 49-52, right 53-56 (angleLeft, angleRight, angleUp, angleDown, radians); 57 headset Hz.
 * i[0]: 1 head, 2 left, 4 right, 8 left aim, 16 right aim, 32 eyes, 64 floor space.
 * ahead: frames past the last one waited for (the frame the game is building now is about one ahead).
 * Returns 1 located, 0 not yet. */
__declspec(dllexport) int XO_Locate(float *f, int *i, int ahead)
{
    for (int k = 0; k < 58; k++) f[k] = 0;
    i[0] = 0;
    i64 t0 = predicted_time, period = predicted_period;
    if (!session || !actions_ready || !t0 || !xr.LocateSpace) return 0;
    if (ahead < 0) ahead = 0; if (ahead > 3) ahead = 3;
    XrTime t = t0 + (period > 0 && period < 100000000 ? period * ahead : 0);
    XrPosef p;
    if (locate(view_space, t, &p)) { put(f, &p); i[0] |= 1; }
    for (int h = 0; h < 2; h++) {
        if (locate(grip_space[h], t, &p)) {
            XrPosef g;
            if (grip_transform(profile[h], h, &g)) {
                Q a = { p.orientation.x, p.orientation.y, p.orientation.z, p.orientation.w }, b = { g.orientation.x, g.orientation.y, g.orientation.z, g.orientation.w };
                XrVector3f d = qrot(a, g.position);
                Q c = qmul(a, b);
                p.position.x += d.x; p.position.y += d.y; p.position.z += d.z;
                p.orientation.x = c.x; p.orientation.y = c.y; p.orientation.z = c.z; p.orientation.w = c.w;
            }
            put(f + 7 + 7 * h, &p); i[0] |= 2 << h;
        }
        if (locate(aim_space[h], t, &p)) { put(f + 21 + 7 * h, &p); i[0] |= 8 << h; }
    }
    if (view_space && xr.LocateViews) {
        XrViewLocateInfo vi = { XR_TYPE_VIEW_LOCATE_INFO };
        vi.viewConfigurationType = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO; vi.displayTime = t; vi.space = view_space;
        XrViewState vs = { XR_TYPE_VIEW_STATE };
        XrView views[2] = { { XR_TYPE_VIEW }, { XR_TYPE_VIEW } };
        uint32_t n = 0;
        if (xr.LocateViews(session, &vi, &vs, 2, &n, views) == XR_SUCCESS && n == 2
            && (vs.viewStateFlags & XR_VIEW_STATE_ORIENTATION_VALID_BIT) && (vs.viewStateFlags & XR_VIEW_STATE_POSITION_VALID_BIT)) {
            for (int e = 0; e < 2; e++) {
                put(f + 35 + 7 * e, &views[e].pose);
                f[49 + 4 * e] = views[e].fov.angleLeft; f[50 + 4 * e] = views[e].fov.angleRight;
                f[51 + 4 * e] = views[e].fov.angleUp; f[52 + 4 * e] = views[e].fov.angleDown;
            }
            i[0] |= 32;
        }
    }
    if (stage) i[0] |= 64;
    if (period > 0) f[57] = 1e9f / (float)period;
    return 1;
}

/* Vibration: hand 0 left, 1 right; amplitude 0..1; seconds (0: the shortest the runtime has). */
__declspec(dllexport) int XO_Vibrate(int hand, float amplitude, float seconds)
{
    if (!session || !attached || !xr.Haptic || hand < 0 || hand > 1) return 0;
    if (!(amplitude > 0)) return 0; if (amplitude > 1) amplitude = 1;
    XrHapticActionInfo hi = { XR_TYPE_HAPTIC_ACTION_INFO };
    hi.action = actions[A_HAPTIC]; hi.subactionPath = hand_path[hand];
    XrHapticVibration v = { XR_TYPE_HAPTIC_VIBRATION };
    v.amplitude = amplitude; v.frequency = XR_FREQUENCY_UNSPECIFIED;
    v.duration = seconds > 0 ? (XrDuration)(seconds * 1e9f) : XR_MIN_HAPTIC_DURATION;
    return xr.Haptic(session, &hi, (const XrHapticBaseHeader *)&v) == XR_SUCCESS;
}
__declspec(dllexport) int XO_StopVibration(int hand)
{
    if (!session || !attached || !xr.StopHaptic || hand < 0 || hand > 1) return 0;
    XrHapticActionInfo hi = { XR_TYPE_HAPTIC_ACTION_INFO };
    hi.action = actions[A_HAPTIC]; hi.subactionPath = hand_path[hand];
    return xr.StopHaptic(session, &hi) == XR_SUCCESS;
}
