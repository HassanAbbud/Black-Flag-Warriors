using Ahoy.Enemies;
using Ahoy.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace Ahoy.Tools
{
    // Makes Golem_Setup (the Portal_1 guardian in IslandMap_Level) a full weak-point boss.
    // Run Tools > Ahoy > Set Up Golem Boss. Running it again rebuilds the controller and the HUD.
    //   1. AC_Golem.controller, following MonsterAnimatorIds: Speed, AttackSpeed and StunSpeed;
    //      Locomotion (Idle/Walk blend, default), Slam, Sweep, Stagger (Golem_Stunned, slowed by
    //      StunSpeed) and Death. The pack's FreeGolem.controller is left alone.
    //   2. ATK_Golem_Slam / ATK_Golem_Sweep get their state names, and Slam plays at 1.25x so the
    //      clip's impact (1.75 s) lands at the end of its 1.4 s wind-up.
    //   3. MON_Golem gets the stagger clip, so BossController stretches the hit across the stun.
    //   4. Golem_Setup: the Animator is wired to BossController, StubWeakPoint is replaced by
    //      WeakPointGauge, and a world-space WeakPointGaugeView floats above its head.
    // Root motion stays off: BossController moves the CharacterController itself.
    static class GolemBossSetup
    {
        const string GolemPrefab = "Assets/Prefabs/Enemy/Golem_Setup.prefab";
        const string ControllerPath = "Assets/Animations/Monsters/AC_Golem.controller";
        const string ClipFolder = "Assets/Models/Monsters/Golem/Animations";
        const string StunnedClip = "Assets/Animations/Monsters/Golem_Stunned.anim";
        const string SlamAttack = "Assets/Data/Enemies/Bosses/ATK_Golem_Slam.asset";
        const string SweepAttack = "Assets/Data/Enemies/Bosses/ATK_Golem_Sweep.asset";
        const string GolemDef = "Assets/Data/Enemies/Bosses/MON_Golem.asset";
        const string HudName = "WeakPoint HUD";

        const string SlamState = "Slam";
        const string SweepState = "Sweep";
        // Attack01's impact frame and the slam's wind-up, seconds.
        const float SlamClipImpact = 1.75f;

        // Attacks hand back to Locomotion near their end; the pack's clips are set to loop.
        const float ExitTime = 0.95f;
        const float ExitBlendSeconds = 0.2f;

        // The HUD: canvas units are pixels, scaled down to metres.
        const float HudScale = 0.01f;
        const float HudHeightAboveController = 0.7f;
        const float DiamondSize = 48f;
        const float BarWidth = 220f;
        const float GaugeHeight = 18f;
        const float WindowHeight = 6f;

        [MenuItem("Tools/Ahoy/Set Up Golem Boss")]
        static void Setup()
        {
            AnimationClip stunned = AssetDatabase.LoadAssetAtPath<AnimationClip>(StunnedClip);
            var slam = AssetDatabase.LoadAssetAtPath<MonsterAttackDef>(SlamAttack);
            var sweep = AssetDatabase.LoadAssetAtPath<MonsterAttackDef>(SweepAttack);
            var def = AssetDatabase.LoadAssetAtPath<BossDef>(GolemDef);
            if (stunned == null || slam == null || sweep == null || def == null)
            {
                Debug.LogError($"Missing one of {StunnedClip}, {SlamAttack}, {SweepAttack}, {GolemDef}.");
                return;
            }

            AnimatorController controller = BuildController(stunned, def);
            if (controller == null) return;

            SetAttack(slam, SlamState, SlamClipImpact / slam.telegraphSeconds);
            SetAttack(sweep, SweepState, 1f);

            var serializedDef = new SerializedObject(def);
            serializedDef.FindProperty("staggerClip").objectReferenceValue = stunned;
            serializedDef.ApplyModifiedPropertiesWithoutUndo();

            if (!WirePrefab(controller)) return;
            AssetDatabase.SaveAssets();
            Debug.Log($"Golem boss set up: {ControllerPath} built and {GolemPrefab} wired.", controller);
        }

        static AnimatorController BuildController(AnimationClip stunned, BossDef def)
        {
            AnimationClip idle = Clip("Idle");
            AnimationClip walk = Clip("Walk");
            AnimationClip attack01 = Clip("Attack01");
            AnimationClip attack02 = Clip("Attack02");
            AnimationClip die = Clip("Die");
            if (!idle || !walk || !attack01 || !attack02 || !die) return null;

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(FloatOne("AttackSpeed"));
            controller.AddParameter(FloatOne("StunSpeed"));
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, def.moveSpeed);
            machine.defaultState = locomotion;

            AddAttack(machine, SlamState, attack01, locomotion);
            AddAttack(machine, SweepState, attack02, locomotion);

            // Held until BossController leaves the stun; it plays once, slowed, then holds.
            AnimatorState stagger = machine.AddState("Stagger");
            stagger.motion = stunned;
            stagger.speedParameter = "StunSpeed";
            stagger.speedParameterActive = true;

            machine.AddState("Death").motion = die;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorControllerParameter FloatOne(string name) => new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        };

        static void AddAttack(AnimatorStateMachine machine, string name, AnimationClip clip, AnimatorState back)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            state.speedParameter = "AttackSpeed";
            state.speedParameterActive = true;
            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = ExitTime;
            exit.hasFixedDuration = true;
            exit.duration = ExitBlendSeconds;
        }

        static AnimationClip Clip(string name)
        {
            string path = $"{ClipFolder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) Debug.LogError($"No clip at {path}.");
            return clip;
        }

        static void SetAttack(MonsterAttackDef attack, string state, float playbackSpeed)
        {
            var serialized = new SerializedObject(attack);
            serialized.FindProperty("animatorState").stringValue = state;
            serialized.FindProperty("playbackSpeed").floatValue = playbackSpeed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static bool WirePrefab(AnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GolemPrefab);
            try
            {
                var boss = root.GetComponentInChildren<BossController>(true);
                var animator = root.GetComponentInChildren<Animator>(true);
                if (boss == null || animator == null)
                {
                    Debug.LogError($"{GolemPrefab} needs a BossController and an Animator.");
                    return false;
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var serializedBoss = new SerializedObject(boss);
                serializedBoss.FindProperty("animator").objectReferenceValue = animator;
                serializedBoss.ApplyModifiedPropertiesWithoutUndo();
                Object balance = serializedBoss.FindProperty("balance").objectReferenceValue;

                // The stub stood in for the gauge; two IWeakPoints would fight over the boss.
                foreach (var stub in boss.GetComponents<Ahoy.Enemies.Testing.StubWeakPoint>())
                    Object.DestroyImmediate(stub, true);

                WeakPointGauge gauge = boss.GetComponent<WeakPointGauge>();
                if (gauge == null) gauge = boss.gameObject.AddComponent<WeakPointGauge>();
                var serializedGauge = new SerializedObject(gauge);
                serializedGauge.FindProperty("balance").objectReferenceValue = balance;
                serializedGauge.FindProperty("boss").objectReferenceValue = boss;
                serializedGauge.ApplyModifiedPropertiesWithoutUndo();

                var body = boss.GetComponent<CharacterController>();
                float top = body != null ? body.center.y + body.height * 0.5f : 4f;
                BuildHud(boss.transform, top + HudHeightAboveController, balance);

                PrefabUtility.SaveAsPrefabAsset(root, GolemPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Canvas > Root > (Diamond, Gauge > (Back, Fill, Window)). The view sits on the canvas and
        // hides Root, so it keeps updating while the weak point is hidden.
        static void BuildHud(Transform boss, float height, Object balance)
        {
            Transform old = boss.Find(HudName);
            if (old != null) Object.DestroyImmediate(old.gameObject, true);

            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject(HudName, typeof(RectTransform), typeof(Canvas), typeof(WeakPointGaugeView));
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.SetParent(boss, false);
            canvasRect.localPosition = Vector3.up * height;
            canvasRect.localScale = Vector3.one * HudScale;
            canvasRect.sizeDelta = new Vector2(BarWidth, DiamondSize + GaugeHeight + WindowHeight);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            RectTransform hudRoot = Rect("Root", canvasRect, Vector2.zero, canvasRect.sizeDelta);

            Image diamond = Box("Diamond", hudRoot, new Vector2(0f, (GaugeHeight + WindowHeight) * 0.5f),
                                new Vector2(DiamondSize, DiamondSize), sprite, new Color(1f, 0.8f, 0.2f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            float barY = -DiamondSize * 0.5f;
            RectTransform group = Rect("Gauge", hudRoot, new Vector2(0f, barY), new Vector2(BarWidth, GaugeHeight + WindowHeight));
            Box("Back", group, new Vector2(0f, WindowHeight * 0.5f), new Vector2(BarWidth, GaugeHeight),
                sprite, new Color(0f, 0f, 0f, 0.7f));
            Image fill = Box("Fill", group, new Vector2(0f, WindowHeight * 0.5f), new Vector2(BarWidth, GaugeHeight),
                             sprite, new Color(1f, 0.8f, 0.2f));
            Filled(fill);
            Image window = Box("Window", group, new Vector2(0f, -GaugeHeight * 0.5f), new Vector2(BarWidth, WindowHeight),
                               sprite, Color.white);
            Filled(window);

            var view = new SerializedObject(canvasGo.GetComponent<WeakPointGaugeView>());
            view.FindProperty("balance").objectReferenceValue = balance;
            view.FindProperty("root").objectReferenceValue = hudRoot.gameObject;
            view.FindProperty("diamond").objectReferenceValue = diamond;
            view.FindProperty("gaugeFill").objectReferenceValue = fill;
            view.FindProperty("windowFill").objectReferenceValue = window;
            view.FindProperty("gaugeGroup").objectReferenceValue = group.gameObject;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            RectTransform rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Filled(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
        }
    }
}
