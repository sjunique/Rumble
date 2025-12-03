// Assets/Editor/QuestgiverAnimatorSetup.cs
// One-click builder for a Jammo Quest-Giver Animator.
// • Full-body or Upper-body-masked "Talk" (toggle).
// • Optional Celebrate / Decline / Wave one-shots.
// • Optional Walk/Run locomotion (MoveSpeed).
// • Creates/updates controller + (optionally) an upper-body AvatarMask.
//
// Menu: Tools ▸ Questgiver ▸ Build Animator (Jammo)
// Select Jammo (Animator) to auto-assign after build.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class QuestgiverAnimatorSetup : EditorWindow
{
    // ----- Inputs -----
    [Header("Rig (for mask)")]
    public GameObject rigRoot; // Jammo in scene (only needed if you want the mask auto-built)

    [Header("Clips")]
    public AnimationClip idleClip;
    public AnimationClip walkClip;      // optional
    public AnimationClip runClip;       // optional
    public List<AnimationClip> talkClips = new();  // one or many
    public AnimationClip celebrateClip; // optional
    public AnimationClip declineClip;   // optional
    public AnimationClip waveClip;      // optional

    [Header("Build Options")]
    public TalkMode talkMode = TalkMode.FullBody;
    public bool baseIKPass = true;              // IK Pass on base layer
    public bool addLocomotion = true;           // adds a Locomotion blend tree (MoveSpeed)
    public bool autoAssignToSelected = true;    // assign to selected Animator after build

    [Header("Asset Paths")]
    public string controllerPath = "Assets/JammoAutoBackup/Jammo_QuestGiver.controller";
    public string maskPath       = "Assets/JammoAutoBackup/Jammo_UpperBody.mask"; // only if talkMode == UpperBodyMasked

    const string ParamTalking     = "Talking";     // Bool
    const string ParamCelebrate   = "Celebrate";   // Trigger
    const string ParamDecline     = "Decline";     // Trigger
    const string ParamWave        = "Wave";        // Trigger
    const string ParamMoveSpeed   = "MoveSpeed";   // Float
    const string ParamTalkAmount  = "TalkAmount";  // Float (for talk blend tree)

public enum TalkMode { FullBody, UpperBodyMasked }

    [MenuItem("Tools/Questgiver/Build Animator (Jammo)")]
    public static void Open() => GetWindow<QuestgiverAnimatorSetup>("Questgiver Animator");

    void OnGUI()
    {
        GUILayout.Label("Jammo Quest-Giver Animator", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            rigRoot = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Rig (for Mask)"), rigRoot, typeof(GameObject), true);

            GUILayout.Space(6);
            GUILayout.Label("Clips", EditorStyles.boldLabel);
            idleClip      = (AnimationClip)EditorGUILayout.ObjectField("Idle (required)", idleClip, typeof(AnimationClip), false);
            walkClip      = (AnimationClip)EditorGUILayout.ObjectField("Walk (optional)", walkClip, typeof(AnimationClip), false);
            runClip       = (AnimationClip)EditorGUILayout.ObjectField("Run (optional)", runClip, typeof(AnimationClip), false);
            celebrateClip = (AnimationClip)EditorGUILayout.ObjectField("Celebrate (optional)", celebrateClip, typeof(AnimationClip), false);
            declineClip   = (AnimationClip)EditorGUILayout.ObjectField("Decline (optional)", declineClip, typeof(AnimationClip), false);
            waveClip      = (AnimationClip)EditorGUILayout.ObjectField("Wave (optional)", waveClip, typeof(AnimationClip), false);

            GUILayout.Space(2);
            EditorGUILayout.LabelField("Talk Clips (one or more):");
            int removeAt = -1;
            for (int i = 0; i < talkClips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                talkClips[i] = (AnimationClip)EditorGUILayout.ObjectField($"  Talk {i+1}", talkClips[i], typeof(AnimationClip), false);
                if (GUILayout.Button("X", GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) talkClips.RemoveAt(removeAt);
            if (GUILayout.Button("Add Talk Clip")) talkClips.Add(null);
        }

        GUILayout.Space(6);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Build Options", EditorStyles.boldLabel);
            talkMode      = (TalkMode)EditorGUILayout.EnumPopup("Talk Mode", talkMode);
            baseIKPass    = EditorGUILayout.Toggle("Base Layer IK Pass", baseIKPass);
            addLocomotion = EditorGUILayout.Toggle("Add Locomotion (MoveSpeed)", addLocomotion);
            autoAssignToSelected = EditorGUILayout.Toggle("Assign To Selected", autoAssignToSelected);
        }

        GUILayout.Space(6);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Asset Paths", EditorStyles.boldLabel);
            controllerPath = EditorGUILayout.TextField("Controller Path", controllerPath);
            using (new EditorGUI.DisabledScope(talkMode != TalkMode.UpperBodyMasked))
                maskPath = EditorGUILayout.TextField("Upper-Body Mask Path", maskPath);
        }

        GUILayout.Space(10);

        using (new EditorGUI.DisabledScope(idleClip == null || talkClips.Count == 0 || talkClips.TrueForAll(c => c == null)))
        {
            if (GUILayout.Button("Build Animator"))
                BuildAnimator();
        }
    }

    // ---- Build ----
    void BuildAnimator()
    {
        EnsureFoldersForAsset(controllerPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (!ctrl) ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        else
        {
            // clear layers
            for (int i = ctrl.layers.Length - 1; i >= 0; i--) ctrl.RemoveLayer(i);
        }

        // Parameters
        AddParam(ctrl, ParamTalking,    AnimatorControllerParameterType.Bool);
        AddParam(ctrl, ParamCelebrate,  AnimatorControllerParameterType.Trigger);
        AddParam(ctrl, ParamDecline,    AnimatorControllerParameterType.Trigger);
        AddParam(ctrl, ParamWave,       AnimatorControllerParameterType.Trigger);
        AddParam(ctrl, ParamMoveSpeed,  AnimatorControllerParameterType.Float);
        AddParam(ctrl, ParamTalkAmount, AnimatorControllerParameterType.Float);

        // Base Layer
        var baseSM = new AnimatorStateMachine { name = "BaseSM" };
        var baseLayer = new AnimatorControllerLayer
        {
            name = "Base",
            defaultWeight = 1f,
            stateMachine = baseSM,
            iKPass = baseIKPass
        };
        ctrl.AddLayer(baseLayer);

        // Idle
        var stIdle = baseSM.AddState("Idle");
        stIdle.motion = idleClip;
        baseSM.defaultState = stIdle;

        // Locomotion (optional)
        AnimatorState stLocomotion = null;
        if (addLocomotion && (walkClip || runClip))
        {
            var bt = new BlendTree { name = "Locomotion", hideFlags = HideFlags.HideInHierarchy };
            bt.blendType = BlendTreeType.Simple1D;
            bt.blendParameter = ParamMoveSpeed;
            bt.useAutomaticThresholds = false;

            float t = 0f;
            if (idleClip) { bt.AddChild(idleClip, t); t = 0.1f; }
            if (walkClip) { bt.AddChild(walkClip, 0.5f); }
            if (runClip)  { bt.AddChild(runClip,  1.0f); }

            AssetDatabase.AddObjectToAsset(bt, controllerPath);
            stLocomotion = baseSM.AddState("Locomotion");
            stLocomotion.motion = bt;

            // Optional transition between Idle <-> Locomotion based on MoveSpeed
      var toLoc  = stIdle.AddTransition(stLocomotion);
toLoc.hasExitTime = false; toLoc.duration = 0.1f;
toLoc.AddCondition(AnimatorConditionMode.Greater, 0.05f, ParamMoveSpeed);

var toIdle = stLocomotion.AddTransition(stIdle);
toIdle.hasExitTime = false; toIdle.duration = 0.1f;
toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, ParamMoveSpeed);
        }

        // Wave (optional)
        if (waveClip)
        {
            var stWave = baseSM.AddState("Wave");
            stWave.motion = waveClip;

       // AnyState is OK:
var anyToWave = baseSM.AddAnyStateTransition(stWave);
anyToWave.hasExitTime = false; anyToWave.duration = 0.05f;
anyToWave.AddCondition(AnimatorConditionMode.If, 0, ParamWave);

// Back to Idle — from the state:
var waveToIdle = stWave.AddTransition(stIdle);
waveToIdle.hasExitTime = true; waveToIdle.exitTime = 0.95f; waveToIdle.duration = 0.05f;

        }

        // Celebrate / Decline (optional one-shots)
 // --- Celebrate (optional) ---
if (celebrateClip)
{
    var stCel = baseSM.AddState("Celebrate");
    stCel.motion = celebrateClip;

    // AnyState -> Celebrate (Trigger)
    var anyToCel = baseSM.AddAnyStateTransition(stCel);
    anyToCel.hasExitTime = false; anyToCel.duration = 0.05f;
    anyToCel.AddCondition(AnimatorConditionMode.If, 0, ParamCelebrate);

    // Celebrate -> Idle (exit time)
    var celBack = stCel.AddTransition(stIdle);
    celBack.hasExitTime = true; celBack.exitTime = 0.95f; celBack.duration = 0.05f;
}

// --- Decline (optional) ---
if (declineClip)
{
    var stDec = baseSM.AddState("Decline");
    stDec.motion = declineClip;

    // AnyState -> Decline (Trigger)
    var anyToDec = baseSM.AddAnyStateTransition(stDec);
    anyToDec.hasExitTime = false; anyToDec.duration = 0.05f;
    anyToDec.AddCondition(AnimatorConditionMode.If, 0, ParamDecline);

    // Decline -> Idle (exit time)
    var decBack = stDec.AddTransition(stIdle);
    decBack.hasExitTime = true; decBack.exitTime = 0.95f; decBack.duration = 0.05f;
}

        // ---- TALK ----
        // Build a Talk BlendTree from talkClips (if multiple). If only one, use that clip.
        Motion talkMotion = null;
        AnimationClip firstTalk = FirstNonNull(talkClips);
        if (firstTalk)
        {
            if (CountNonNull(talkClips) == 1)
            {
                talkMotion = firstTalk;
            }
            else
            {
                var talkBT = new BlendTree { name = "TalkBT", hideFlags = HideFlags.HideInHierarchy };
                talkBT.blendParameter = ParamTalkAmount;
                talkBT.blendType = BlendTreeType.Simple1D;
                talkBT.useAutomaticThresholds = true;
                foreach (var c in talkClips) if (c) talkBT.AddChild(c);
                AssetDatabase.AddObjectToAsset(talkBT, controllerPath);
                talkMotion = talkBT;
            }
        }

        if (talkMode == TalkMode.FullBody)
        {
            // Full-body Talk state on base layer
            var stTalk = baseSM.AddState("Talk");
            stTalk.motion = talkMotion;

      var anyToTalk = baseSM.AddAnyStateTransition(stTalk);
anyToTalk.hasExitTime = false; anyToTalk.duration = 0.1f;
anyToTalk.AddCondition(AnimatorConditionMode.If, 0, ParamTalking);

// Talk → Idle when Talking == false (from state):
var talkBack = stTalk.AddTransition(stIdle);
talkBack.hasExitTime = true; talkBack.exitTime = 0.05f; talkBack.duration = 0.1f;
talkBack.AddCondition(AnimatorConditionMode.IfNot, 0, ParamTalking);
        }
        else
        {
            // Upper-body masked Talk layer
            var mask = GetOrCreateUpperBodyMask(maskPath, rigRoot);
            var ubSM = new AnimatorStateMachine { name = "TalkSM" };
            var ubLayer = new AnimatorControllerLayer
            {
                name = "UpperBodyTalk",
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
                stateMachine = ubSM
            };
            ctrl.AddLayer(ubLayer);

            var ubIdle = ubSM.AddState("UB_Idle");
            ubIdle.motion = idleClip; // harmless filler

            ubSM.defaultState = ubIdle;

            var ubTalk = ubSM.AddState("UB_Talk");
            ubTalk.motion = talkMotion;

     var toTalk = ubIdle.AddTransition(ubTalk);
toTalk.hasExitTime = false; toTalk.duration = 0.1f;
toTalk.AddCondition(AnimatorConditionMode.If, 0, ParamTalking);

var back = ubTalk.AddTransition(ubIdle);
back.hasExitTime = true; back.exitTime = 0.05f; back.duration = 0.1f;
back.AddCondition(AnimatorConditionMode.IfNot, 0, ParamTalking);

        }

        // Save
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Auto-assign to selected
        if (autoAssignToSelected && Selection.activeGameObject)
        {
            var anim = Selection.activeGameObject.GetComponent<Animator>();
            if (anim) { anim.runtimeAnimatorController = ctrl; EditorUtility.SetDirty(anim); }
        }

        Debug.Log($"[QuestgiverAnimatorSetup] Built {controllerPath}  (TalkMode={talkMode})");
    }

    // ----- Helpers -----
    static void AddParam(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in c.parameters) if (p.name == name && p.type == type) return;
        c.AddParameter(name, type);
    }

    static AnimationClip FirstNonNull(List<AnimationClip> list)
    {
        foreach (var c in list) if (c) return c;
        return null;
    }
    static int CountNonNull(List<AnimationClip> list)
    {
        int n = 0; foreach (var c in list) if (c) n++; return n;
    }

    static void EnsureFoldersForAsset(string assetPath)
    {
        var path = assetPath.Replace('\\','/');
        int i = path.LastIndexOf('/');
        if (i <= 0) return;
        string folder = path.Substring(0, i);
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string cur = parts[0];
        for (int p = 1; p < parts.Length; p++)
        {
            string next = $"{cur}/{parts[p]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[p]);
            cur = next;
        }
    }

    static AvatarMask GetOrCreateUpperBodyMask(string maskAssetPath, GameObject rig)
    {
        EnsureFoldersForAsset(maskAssetPath);

        // If exists, return
        var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskAssetPath);
        if (existing) return existing;

        // Build a transform-based mask (no AvatarMaskBodyPart enums)
        var mask = new AvatarMask();

        // If no rig provided, create an empty mask (user can replace later)
        if (!rig)
        {
            AssetDatabase.CreateAsset(mask, maskAssetPath);
            return mask;
        }

        var keep = new List<Transform>();
        string[] needles =
        {
            "spine","chest","upperchest","neck","head",
            "shoulder","clavicle","collar","upperarm","lowerarm","forearm","hand"
        };

        void Walk(Transform t)
        {
            string n = t.name.ToLowerInvariant();
            for (int k = 0; k < needles.Length; k++)
            {
                if (n.Contains(needles[k])) { keep.Add(t); break; }
            }
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i));
        }
        Walk(rig.transform);

        mask.transformCount = keep.Count;
        for (int i = 0; i < keep.Count; i++)
        {
            string path = AnimationUtility.CalculateTransformPath(keep[i], rig.transform);
            mask.SetTransformPath(i, path);
            mask.SetTransformActive(i, true);
        }

        AssetDatabase.CreateAsset(mask, maskAssetPath);
        return mask;
    }
}
#endif

