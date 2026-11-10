using Ahoy.Feel;
using Ahoy.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ahoy.Tools
{
    // Wires the boss open-stage feel. Run Tools > Ahoy > Set Up Boss Open-Stage Feel with the
    // test scene open. Safe to run again: it only adds what is missing.
    //   1. VFX_OpenHit, VFX_Break and VFX_Smash: scaled-up variants of VFX_HitHeavy.
    //   2. Zahim.prefab gets OpenStageFeedback, wired to the same balance, attack, shake and
    //      hitstop as CombatFeedback, plus three VfxPools for the bursts.
    //   3. The MainCamera in every open scene gets CameraZoom, post-processing on, and a child
    //      global Volume (VP_OpenStage: vignette, contrast, chromatic aberration) at weight 0.
    static class OpenStageFeelSetup
    {
        const string ZahimPrefab = "Assets/Prefabs/Player/Zahim.prefab";
        const string HeavyHitPrefab = "Assets/Prefabs/VFX/VFX_HitHeavy.prefab";
        const string VfxFolder = "Assets/Prefabs/VFX";
        const string ProfilePath = "Assets/Data/Feel/VP_OpenStage.asset";
        const string VfxRootName = "[OpenStage VFX]";
        const string VolumeName = "OpenStage Volume";

        [MenuItem("Tools/Ahoy/Set Up Boss Open-Stage Feel")]
        static void Setup()
        {
            ParticleSystem openHit = MakeBurst("VFX_OpenHit", 1.6f);
            ParticleSystem breakBurst = MakeBurst("VFX_Break", 2.6f);
            ParticleSystem smashBurst = MakeBurst("VFX_Smash", 3.6f);
            if (openHit == null || breakBurst == null || smashBurst == null) return;

            Object balance = WireZahim(openHit, breakBurst, smashBurst);
            if (balance == null) return;

            int cameras = WireCameras(balance);
            AssetDatabase.SaveAssets();
            Debug.Log($"Boss open-stage feel wired into {ZahimPrefab} and {cameras} camera(s). " +
                      "Save the open scene(s) to keep the camera changes.");
        }

        // A variant of the heavy hit sparks, scaled up. Hierarchy scaling makes the particles
        // themselves grow with the root, not just the emitter.
        static ParticleSystem MakeBurst(string name, float scale)
        {
            string path = $"{VfxFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HeavyHitPrefab);
            if (source == null)
            {
                Debug.LogError($"No {HeavyHitPrefab} to build {name} from.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = name;
                instance.transform.localScale = Vector3.one * scale;
                foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = system.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                return saved.GetComponent<ParticleSystem>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static Object WireZahim(ParticleSystem openHit, ParticleSystem breakBurst, ParticleSystem smashBurst)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ZahimPrefab);
            try
            {
                var feedback = root.GetComponentInChildren<CombatFeedback>(true);
                var attack = root.GetComponentInChildren<PlayerAttack>(true);
                if (feedback == null || attack == null)
                {
                    Debug.LogError($"{ZahimPrefab} has no CombatFeedback or PlayerAttack.");
                    return null;
                }

                var combat = new SerializedObject(feedback);
                Object balance = combat.FindProperty("balance").objectReferenceValue;
                Object shake = combat.FindProperty("shake").objectReferenceValue;
                Object hitstop = new SerializedObject(attack).FindProperty("hitstop").objectReferenceValue;
                if (balance == null || shake == null || hitstop == null)
                {
                    Debug.LogError("CombatFeedback needs balance and shake, and PlayerAttack needs hitstop, before this can wire them.");
                    return null;
                }

                OpenStageFeedback open = feedback.GetComponent<OpenStageFeedback>();
                if (open == null) open = feedback.gameObject.AddComponent<OpenStageFeedback>();

                Transform vfxRoot = root.transform.Find(VfxRootName);
                if (vfxRoot == null)
                {
                    vfxRoot = new GameObject(VfxRootName).transform;
                    vfxRoot.SetParent(root.transform, false);
                }
                VfxPool[] pools = vfxRoot.GetComponents<VfxPool>();
                VfxPool openPool = Pool(vfxRoot, pools, 0, openHit, balance);
                VfxPool breakPool = Pool(vfxRoot, pools, 1, breakBurst, balance);
                VfxPool smashPool = Pool(vfxRoot, pools, 2, smashBurst, balance);

                var serialized = new SerializedObject(open);
                serialized.FindProperty("balance").objectReferenceValue = balance;
                serialized.FindProperty("attack").objectReferenceValue = attack;
                serialized.FindProperty("lockOn").objectReferenceValue = root.GetComponentInChildren<LockOn>(true);
                serialized.FindProperty("hitstop").objectReferenceValue = hitstop;
                serialized.FindProperty("shake").objectReferenceValue = shake;
                serialized.FindProperty("openHit").objectReferenceValue = openPool;
                serialized.FindProperty("breakBurst").objectReferenceValue = breakPool;
                serialized.FindProperty("smashBurst").objectReferenceValue = smashPool;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ZahimPrefab);
                return balance;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static VfxPool Pool(Transform host, VfxPool[] existing, int index, ParticleSystem prefab, Object balance)
        {
            VfxPool pool = index < existing.Length ? existing[index] : host.gameObject.AddComponent<VfxPool>();
            var serialized = new SerializedObject(pool);
            serialized.FindProperty("balance").objectReferenceValue = balance;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return pool;
        }

        static int WireCameras(Object balance)
        {
            VolumeProfile profile = Profile();
            int wired = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject top in scene.GetRootGameObjects())
                {
                    foreach (Camera camera in top.GetComponentsInChildren<Camera>(true))
                    {
                        if (!camera.CompareTag("MainCamera")) continue;
                        WireCamera(camera, balance, profile);
                        EditorSceneManager.MarkSceneDirty(scene);
                        wired++;
                    }
                }
            }
            if (wired == 0) Debug.LogWarning("No MainCamera-tagged camera in the open scenes. Open your test scene and run this again.");
            return wired;
        }

        static void WireCamera(Camera camera, Object balance, VolumeProfile profile)
        {
            CameraZoom zoom = camera.GetComponent<CameraZoom>();
            if (zoom == null) zoom = Undo.AddComponent<CameraZoom>(camera.gameObject);

            // The Volume only shows when the camera renders post-processing.
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                Undo.RecordObject(data, "Enable post-processing");
                data.renderPostProcessing = true;
            }

            Transform child = camera.transform.Find(VolumeName);
            Volume volume = child != null ? child.GetComponent<Volume>() : null;
            if (volume == null)
            {
                var go = new GameObject(VolumeName);
                Undo.RegisterCreatedObjectUndo(go, "Create open-stage Volume");
                go.transform.SetParent(camera.transform, false);
                volume = go.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;   // CameraZoom fades it in while a boss is open
            volume.sharedProfile = profile;

            var serialized = new SerializedObject(zoom);
            serialized.FindProperty("balance").objectReferenceValue = balance;
            serialized.FindProperty("volume").objectReferenceValue = volume;
            serialized.ApplyModifiedProperties();
        }

        static VolumeProfile Profile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.6f);
            vignette.color.Override(new Color(0.15f, 0f, 0f));

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(25f);
            color.saturation.Override(20f);

            ChromaticAberration aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0.35f);

            // Volume components live as sub-assets of the profile.
            foreach (VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
