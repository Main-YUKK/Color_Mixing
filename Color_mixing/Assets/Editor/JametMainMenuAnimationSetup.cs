using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Menu Tools > Color Mixing > Setup Jamet Main Menu Animation.
// Sekali jalan: membangun animasi jamet di MainMenu.unity
//   JametGroup (Animator)  ← slide-in sekali (JametEnter) lalu kilau pomade loop (JametShine)
//   ├─ Jamet (Image + Mask)
//   │  └─ ShineBar
//   ├─ Ting
//   └─ Sparkles / Sparkle1..3
// Kalau JametGroup sudah ada, script berhenti tanpa mengubah apa pun.
public static class JametMainMenuAnimationSetup
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SpriteFolder = "Assets/Models/UI/MainMenu";
    private const string AnimFolder = "Assets/Animations/MainMenu";
    private const string ShineSpritePath = SpriteFolder + "/shineBar.png";
    private const string SparkleSpritePath = SpriteFolder + "/sparkle.png";
    private const string TingSpritePath = SpriteFolder + "/tingGlow.png";

    // Timing (detik)
    private const float SlideOvershootTime = 0.63f;
    private const float SlideEndTime = 0.90f;
    private const float ShineLoopLength = 3.5f;
    private const float ShineSweepTime = 1.05f;

    // Posisi lokal di dalam JametGroup (959 x 1125)
    private const float ShineStartX = -800f;
    private const float ShineEndX = 800f;
    private const float SlideOffscreenOffset = -1523f; // -677 → -2200: aman untuk iPad terlebar (Constant Pixel Size)
    private const float SlideOvershootOffset = 37f;

    [MenuItem("Tools/Color Mixing/Setup Jamet Main Menu Animation")]
    private static void Setup()
    {
        // 1. Import settings sprite
        foreach (var path in new[] { ShineSpritePath, SparkleSpritePath, TingSpritePath })
        {
            if (!ConfigureSprite(path)) return;
        }
        var shineSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShineSpritePath);
        var sparkleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SparkleSpritePath);
        var tingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TingSpritePath);

        // 2. Buka scene MainMenu
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var safeArea = FindInScene("SafeArea");
        if (safeArea == null) { Debug.LogError("[JAMET] SafeArea tidak ditemukan di MainMenu."); return; }
        if (safeArea.Find("JametGroup") != null)
        {
            Debug.LogWarning("[JAMET] JametGroup sudah ada. Setup dibatalkan supaya tidak dobel.");
            return;
        }
        var jamet = safeArea.Find("Jamet") as RectTransform;
        if (jamet == null) { Debug.LogError("[JAMET] SafeArea/Jamet tidak ditemukan."); return; }

        Undo.SetCurrentGroupName("Setup Jamet Main Menu Animation");
        int undoGroup = Undo.GetCurrentGroup();

        // JametGroup mengambil posisi & ukuran Jamet
        var group = CreateUIObject("JametGroup", safeArea);
        group.anchorMin = jamet.anchorMin;
        group.anchorMax = jamet.anchorMax;
        group.pivot = jamet.pivot;
        group.sizeDelta = jamet.sizeDelta;
        group.anchoredPosition = jamet.anchoredPosition;
        group.SetSiblingIndex(jamet.GetSiblingIndex());
        Vector2 restPos = jamet.anchoredPosition;

        // Jamet masuk ke JametGroup, stretch penuh, + Mask
        Undo.SetTransformParent(jamet, group, "Reparent Jamet");
        Undo.RecordObject(jamet, "Stretch Jamet");
        jamet.anchorMin = Vector2.zero;
        jamet.anchorMax = Vector2.one;
        jamet.pivot = new Vector2(0.5f, 0.5f);
        jamet.offsetMin = Vector2.zero;
        jamet.offsetMax = Vector2.zero;
        jamet.localRotation = Quaternion.identity;
        jamet.localScale = Vector3.one;
        var mask = Undo.AddComponent<Mask>(jamet.gameObject);
        mask.showMaskGraphic = true;

        // ShineBar (di dalam Mask)
        var shine = CreateImage("ShineBar", jamet, shineSprite, new Vector2(220, 1700), new Vector2(ShineStartX, 0),
            new Color(1f, 1f, 1f, 120f / 255f));
        shine.localRotation = Quaternion.Euler(0, 0, -15f);

        // Ting (di luar Mask supaya glow tidak terpotong)
        var ting = CreateImage("Ting", group, tingSprite, new Vector2(300, 300), new Vector2(-190, 360),
            new Color(1f, 1f, 1f, 0f));
        ting.localScale = new Vector3(0.6f, 0.6f, 1f);

        // Sparkles
        var sparkles = CreateUIObject("Sparkles", group);
        sparkles.anchorMin = Vector2.zero;
        sparkles.anchorMax = Vector2.one;
        sparkles.offsetMin = Vector2.zero;
        sparkles.offsetMax = Vector2.zero;
        var s1 = CreateImage("Sparkle1", sparkles, sparkleSprite, new Vector2(60, 60), new Vector2(-165, 495), Color.white);
        var s2 = CreateImage("Sparkle2", sparkles, sparkleSprite, new Vector2(50, 50), new Vector2(240, 315), Color.white);
        var s3 = CreateImage("Sparkle3", sparkles, sparkleSprite, new Vector2(45, 45), new Vector2(-290, 90), Color.white);
        foreach (var s in new[] { s1, s2, s3 }) s.localScale = new Vector3(0f, 0f, 1f);

        // 3 & 4. Animation clips
        EnsureFolder(AnimFolder);
        var enterClip = BuildEnterClip(restPos.x);
        var shineClip = BuildShineClip();
        AssetDatabase.CreateAsset(enterClip, AnimFolder + "/JametEnter.anim");
        AssetDatabase.CreateAsset(shineClip, AnimFolder + "/JametShine.anim");

        // 5. Animator Controller: JametEnter (default, sekali) → JametShine (loop)
        var controller = AnimatorController.CreateAnimatorControllerAtPath(AnimFolder + "/JametMainMenu.controller");
        var sm = controller.layers[0].stateMachine;
        var enterState = sm.AddState("JametEnter", new Vector3(260, 120, 0));
        enterState.motion = enterClip;
        var shineState = sm.AddState("JametShine", new Vector3(520, 120, 0));
        shineState.motion = shineClip;
        sm.defaultState = enterState;
        var transition = enterState.AddTransition(shineState);
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.hasFixedDuration = true;
        transition.duration = 0f;

        var animator = Undo.AddComponent<Animator>(group.gameObject);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = group.gameObject;
        EditorGUIUtility.PingObject(group.gameObject);
        Debug.Log("[JAMET] Selesai: JametGroup + JametEnter → JametShine dibuat dan MainMenu disimpan. Tekan Play untuk melihat.");
    }

    // ---------- Clips ----------

    private static AnimationClip BuildEnterClip(float restX)
    {
        var clip = new AnimationClip { name = "JametEnter", frameRate = 60 };

        // Slide-in JametGroup: luar layar → lewat sedikit → posisi asli
        var slide = Curve(0f, restX + SlideOffscreenOffset,
                          SlideOvershootTime, restX + SlideOvershootOffset,
                          SlideEndTime, restX);
        AnimationUtility.SetKeyRightTangentMode(slide, 0, AnimationUtility.TangentMode.Linear); // start cepat, bukan ease-in
        Set(clip, "", typeof(RectTransform), "m_AnchoredPosition.x", slide);

        Set(clip, "", typeof(Transform), "localEulerAnglesRaw.x", Curve(0f, 0f, SlideEndTime, 0f));
        Set(clip, "", typeof(Transform), "localEulerAnglesRaw.y", Curve(0f, 0f, SlideEndTime, 0f));
        Set(clip, "", typeof(Transform), "localEulerAnglesRaw.z", Curve(0f, -8f, SlideOvershootTime, 2f, SlideEndTime, 0f));

        // Ting flash saat mendarat
        var tingScale = Curve(0f, 0.6f, 0.85f, 0.6f, 1.0f, 0.9f, 1.35f, 1.15f);
        Set(clip, "Ting", typeof(Transform), "m_LocalScale.x", tingScale);
        Set(clip, "Ting", typeof(Transform), "m_LocalScale.y", tingScale);
        Set(clip, "Ting", typeof(Image), "m_Color.a", Curve(0f, 0f, 0.85f, 0f, 1.0f, 1f, 1.35f, 0f));

        // Kilau & bintang tetap tersembunyi selama slide-in
        Set(clip, "Jamet/ShineBar", typeof(RectTransform), "m_AnchoredPosition.x", Curve(0f, ShineStartX));
        foreach (var s in new[] { "Sparkles/Sparkle1", "Sparkles/Sparkle2", "Sparkles/Sparkle3" })
        {
            Set(clip, s, typeof(Transform), "m_LocalScale.x", Curve(0f, 0f));
            Set(clip, s, typeof(Transform), "m_LocalScale.y", Curve(0f, 0f));
        }

        SetLoop(clip, false);
        return clip;
    }

    private static AnimationClip BuildShineClip()
    {
        var clip = new AnimationClip { name = "JametShine", frameRate = 60 };

        // Pita kilau menyapu badan, lalu diam di luar sampai loop berikutnya
        var sweep = Curve(0f, ShineStartX, ShineSweepTime, ShineEndX, ShineLoopLength, ShineEndX);
        Set(clip, "Jamet/ShineBar", typeof(RectTransform), "m_AnchoredPosition.x", sweep);

        // Tiap bintang berkedip 2x per loop: 0 → 1 (+0.3s) → 0 (+0.7s), rotasi 0 → 45 → 90
        AddTwinkle(clip, "Sparkles/Sparkle1", 0.25f, 2.00f);
        AddTwinkle(clip, "Sparkles/Sparkle2", 0.55f, 2.30f);
        AddTwinkle(clip, "Sparkles/Sparkle3", 0.85f, 2.60f);

        SetLoop(clip, true);
        return clip;
    }

    private static void AddTwinkle(AnimationClip clip, string path, float startA, float startB)
    {
        var scale = Curve(0f, 0f,
                          startA, 0f, startA + 0.3f, 1f, startA + 0.7f, 0f,
                          startB, 0f, startB + 0.3f, 1f, startB + 0.7f, 0f,
                          ShineLoopLength, 0f);
        Set(clip, path, typeof(Transform), "m_LocalScale.x", scale);
        Set(clip, path, typeof(Transform), "m_LocalScale.y", scale);

        var rot = Curve(0f, 0f,
                        startA, 0f, startA + 0.3f, 45f, startA + 0.7f, 90f,
                        startB, 0f, startB + 0.3f, 45f, startB + 0.7f, 90f,
                        ShineLoopLength, 90f);
        Set(clip, path, typeof(Transform), "localEulerAnglesRaw.x", Curve(0f, 0f, ShineLoopLength, 0f));
        Set(clip, path, typeof(Transform), "localEulerAnglesRaw.y", Curve(0f, 0f, ShineLoopLength, 0f));
        Set(clip, path, typeof(Transform), "localEulerAnglesRaw.z", rot);
    }

    // ---------- Helpers ----------

    // Curve(t0, v0, t1, v1, ...) dengan tangent ClampedAuto (halus, tanpa overshoot palsu)
    private static AnimationCurve Curve(params float[] timeValuePairs)
    {
        var keys = new Keyframe[timeValuePairs.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    private static void Set(AnimationClip clip, string path, System.Type type, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static bool ConfigureSprite(string path)
    {
        if (!File.Exists(path)) { Debug.LogError($"[JAMET] Sprite tidak ditemukan: {path}"); return false; }
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"[JAMET] Bukan texture: {path}"); return false; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return true;
    }

    private static RectTransform CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static RectTransform CreateImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos, Color color)
    {
        var rt = CreateUIObject(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private static Transform FindInScene(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
