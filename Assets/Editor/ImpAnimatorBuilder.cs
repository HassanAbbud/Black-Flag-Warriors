using System.Linq;
using Ahoy.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ahoy.Tools
{
    // Builds the Imp's Animator Controller from the humanoid UAL clips in Assets/Animations and
    // wires it into Imp_Setup. Run Tools > Ahoy > Build Imp Animator. Running it again rebuilds
    // the controller from scratch and rewires the prefab.
    //
    //   1. Imp.fbx is switched to a Humanoid rig, so humanoid clips can retarget onto it.
    //   2. The controller follows MonsterAnimatorIds: Speed and AttackSpeed parameters, and the
    //      states Locomotion (default), HitFlinch, HitKnockback, Stagger and Death, plus Attack
    //      for ATK_Imp_Bite.
    //   3. Imp_Setup's Animator gets the controller and avatar, and Monster gets the Animator.
    //      An AnimationEventSink goes beside it for the events baked into the sword clips.
    // Root motion stays off: Monster moves the transform itself.
    static class ImpAnimatorBuilder
    {
        const string ImpModel = "Assets/Models/Monsters/ImpPuglin/Imp.fbx";
        const string ImpPrefab = "Assets/Prefabs/Enemy/Imp_Setup.prefab";
        const string BiteAttack = "Assets/Data/Enemies/Scuttler/ATK_Imp_Bite.asset";
        const string ControllerFolder = "Assets/Animations/Monsters";
        const string ControllerPath = ControllerFolder + "/AC_Imp.controller";
        static readonly string[] ClipSources =
        {
            "Assets/Animations/UAL1_Standard.fbx",
            "Assets/Animations/UAL2_Standard.fbx",
        };

        const string AttackState = "Attack";

        // Locomotion thresholds, in the m/s Monster writes to Speed. Jog matches MON_Imp's moveSpeed.
        const float WalkSpeed = 1.2f;
        const float JogSpeed = 3.2f;

        // One-shots hand back to Locomotion near their end, in case the code does not first.
        const float ExitTime = 0.9f;
        const float ExitBlendSeconds = 0.15f;

        [MenuItem("Tools/Ahoy/Build Imp Animator")]
        static void Build()
        {
            Avatar avatar = MakeHumanoid(ImpModel);
            if (avatar == null) return;

            AnimatorController controller = BuildController();
            if (controller == null) return;

            if (!WirePrefab(controller, avatar)) return;
            NameAttackState();
            AssetDatabase.SaveAssets();
            Debug.Log($"Imp animator built at {ControllerPath} and wired into {ImpPrefab}.", controller);
        }

        // Humanoid clips only play on a Humanoid avatar. The Imp's skeleton uses the Unreal
        // mannequin names (pelvis, spine_01, thigh_l...), which Unity's auto-mapping knows.
        static Avatar MakeHumanoid(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar != null && avatar.isValid && avatar.isHuman) return avatar;

            Debug.LogError($"{path}: Unity could not map the skeleton to a Humanoid avatar. Select the model, " +
                           "open Rig > Configure, fix the bone mapping, apply, then run this again.", importer);
            return null;
        }

        static AnimatorController BuildController()
        {
            AnimationClip idle = Clip("Idle_Loop");
            AnimationClip walk = Clip("Walk_Loop");
            AnimationClip jog = Clip("Jog_Fwd_Loop");
            AnimationClip flinch = Clip("Hit_Chest");
            AnimationClip knockback = Clip("Hit_Knockback");
            AnimationClip stagger = Clip("Hit_Head");
            AnimationClip death = Clip("Death01");
            AnimationClip attack = Clip("Sword_Regular_A");   // the Imp swings its mace
            if (!idle || !walk || !jog || !flinch || !knockback || !stagger || !death || !attack) return null;

            if (!AssetDatabase.IsValidFolder(ControllerFolder))
                AssetDatabase.CreateFolder("Assets/Animations", "Monsters");
            AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "AttackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkSpeed);
            tree.AddChild(jog, JogSpeed);
            machine.defaultState = locomotion;

            AddOneShot(machine, "HitFlinch", flinch, locomotion);
            AddOneShot(machine, "HitKnockback", knockback, locomotion);
            AddOneShot(machine, "Stagger", stagger, locomotion);
            AnimatorState swing = AddOneShot(machine, AttackState, attack, locomotion);
            swing.speedParameter = "AttackSpeed";
            swing.speedParameterActive = true;

            // Death holds its last frame until the corpse goes back to the pool.
            machine.AddState("Death").motion = death;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState AddOneShot(AnimatorStateMachine machine, string name, AnimationClip clip, AnimatorState back)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = ExitTime;
            exit.hasFixedDuration = true;
            exit.duration = ExitBlendSeconds;
            return state;
        }

        // Clips inside the FBX are named "Armature|Idle_Loop".
        static AnimationClip Clip(string take)
        {
            foreach (string source in ClipSources)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(source))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") &&
                        (clip.name == take || clip.name.EndsWith("|" + take)))
                        return clip;
                }
            }
            Debug.LogError($"No clip named '{take}' in {string.Join(", ", ClipSources)}.");
            return null;
        }

        static bool WirePrefab(AnimatorController controller, Avatar avatar)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ImpPrefab);
            try
            {
                Monster monster = root.GetComponentInChildren<Monster>();
                if (monster == null)
                {
                    Debug.LogError($"{ImpPrefab} has no Monster component.");
                    return false;
                }

                Animator animator = root.GetComponentInChildren<Animator>();
                if (animator == null) animator = monster.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                // A crowd of hundreds: skip bone updates for Imps nobody can see.
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                // The sword clips fire Zahim's attack events; the Imp ignores them.
                if (animator.GetComponent<AnimationEventSink>() == null)
                    animator.gameObject.AddComponent<AnimationEventSink>();

                var serialized = new SerializedObject(monster);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ImpPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ATK_Imp_Bite named no state, so its swing played no clip.
        static void NameAttackState()
        {
            var bite = AssetDatabase.LoadAssetAtPath<MonsterAttackDef>(BiteAttack);
            if (bite == null) return;
            var serialized = new SerializedObject(bite);
            serialized.FindProperty("animatorState").stringValue = AttackState;
            serialized.ApplyModifiedProperties();
        }
    }
}
