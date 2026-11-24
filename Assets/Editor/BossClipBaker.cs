using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ahoy.Tools
{
    // Makes the boss clips a pack does not ship (W7.1): copies an existing clip of the same rig and
    // layers turns onto a few bones, so the rest of the body keeps its own motion.
    // A turn is given in the model's space (yaw = around up, pitch = around its right) and converted
    // into each bone's own axes from the rig's rest pose, so it reads right whatever the rig's axes are.
    static class BossClipBaker
    {
        const float FrameRate = 30f;

        internal struct Turn
        {
            public string bone;
            public Func<float, Vector3> euler;   // seconds -> (pitch, yaw, roll) degrees in model space
        }

        internal static AnimationClip Bake(AnimationClip source, GameObject rig, string path, float seconds, params Turn[] turns)
        {
            var clip = new AnimationClip { frameRate = FrameRate };
            int frames = Mathf.CeilToInt(seconds * FrameRate) + 1;
            float length = Mathf.Max(0.01f, source.length);

            // Rest pose of each turned bone, in the model's space.
            var rest = new Dictionary<string, Quaternion>();
            var restLocal = new Dictionary<string, Quaternion>();
            var edits = new Dictionary<string, Func<float, Vector3>>();
            foreach (Turn t in turns)
            {
                Transform bone = Find(rig.transform, t.bone);
                if (bone == null) { Debug.LogWarning($"[Clips] no bone {t.bone} in {rig.name}"); continue; }
                string bonePath = AnimationUtility.CalculateTransformPath(bone, rig.transform);
                rest[bonePath] = Quaternion.Inverse(rig.transform.rotation) * bone.rotation;
                restLocal[bonePath] = bone.localRotation;
                edits[bonePath] = t.euler;
            }

            var rotations = new Dictionary<string, EditorCurveBinding[]>();
            var eulers = new Dictionary<string, EditorCurveBinding[]>();   // clips made in Unity key Euler angles
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(source))
            {
                int euler = EulerAxis(b.propertyName);
                if (euler >= 0 && edits.ContainsKey(b.path))
                {
                    if (!eulers.TryGetValue(b.path, out var three)) eulers[b.path] = three = new EditorCurveBinding[3];
                    three[euler] = b;
                    continue;
                }
                int axis = Axis(b.propertyName);
                if (axis >= 0 && edits.ContainsKey(b.path))
                {
                    if (!rotations.TryGetValue(b.path, out var four)) rotations[b.path] = four = new EditorCurveBinding[4];
                    four[axis] = b;
                    continue;
                }
                // Everything else plays as in the source, looped to the new length.
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, b);
                var keys = new Keyframe[frames];
                for (int f = 0; f < frames; f++) { float t = f / FrameRate; keys[f] = new Keyframe(t, curve.Evaluate(t % length)); }
                AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(keys));
            }

            // A turned bone the source never moves starts from its rest pose.
            foreach (string bonePath in edits.Keys)
                if (!rotations.ContainsKey(bonePath)) rotations[bonePath] = new EditorCurveBinding[4];

            foreach (var pair in rotations)
            {
                AnimationCurve[] src = new AnimationCurve[4];
                Quaternion still = restLocal[pair.Key];
                float[] stillParts = { still.x, still.y, still.z, still.w };
                for (int i = 0; i < 4; i++)
                    src[i] = pair.Value[i].propertyName != null ? AnimationUtility.GetEditorCurve(source, pair.Value[i]) : AnimationCurve.Constant(0f, length, stillParts[i]);
                // Keyed as Euler angles: those are the base instead of the rest pose.
                AnimationCurve[] eu = null;
                if (eulers.TryGetValue(pair.Key, out var three))
                {
                    eu = new AnimationCurve[3];
                    for (int i = 0; i < 3; i++) eu[i] = three[i].propertyName != null ? AnimationUtility.GetEditorCurve(source, three[i]) : null;
                }
                var keys = new Keyframe[4][];
                for (int i = 0; i < 4; i++) keys[i] = new Keyframe[frames];
                Quaternion restRot = rest[pair.Key];
                for (int f = 0; f < frames; f++)
                {
                    float t = f / FrameRate, s = t % length;
                    var q = eu != null
                        ? Quaternion.Euler(eu[0]?.Evaluate(s) ?? still.eulerAngles.x, eu[1]?.Evaluate(s) ?? still.eulerAngles.y, eu[2]?.Evaluate(s) ?? still.eulerAngles.z)
                        : new Quaternion(src[0]?.Evaluate(s) ?? 0f, src[1]?.Evaluate(s) ?? 0f, src[2]?.Evaluate(s) ?? 0f, src[3]?.Evaluate(s) ?? 1f);
                    Vector3 e = edits[pair.Key](t);
                    Quaternion model = Quaternion.Euler(e.x, e.y, e.z);
                    q = q * (Quaternion.Inverse(restRot) * model * restRot);   // a model-space turn, in the bone's axes
                    keys[0][f] = new Keyframe(t, q.x); keys[1][f] = new Keyframe(t, q.y); keys[2][f] = new Keyframe(t, q.z); keys[3][f] = new Keyframe(t, q.w);
                }
                string[] names = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int i = 0; i < 4; i++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pair.Key, typeof(Transform), names[i]), new AnimationCurve(keys[i]));
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        internal struct Muscle
        {
            public string name;              // a humanoid muscle, e.g. "Head Turn Left-Right"
            public Func<float, float> add;   // seconds -> amount added (muscle units, -1..1)
        }

        // Humanoid rigs play muscle curves, not bone rotations: the same idea through the muscles.
        internal static AnimationClip BakeMuscles(AnimationClip source, string path, float seconds, params Muscle[] muscles)
        {
            var clip = new AnimationClip { frameRate = FrameRate };
            int frames = Mathf.CeilToInt(seconds * FrameRate) + 1;
            float length = Mathf.Max(0.01f, source.length);
            var pending = new Dictionary<string, Func<float, float>>();
            foreach (Muscle m in muscles) pending[m.name] = m.add;

            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, b);
                pending.TryGetValue(b.propertyName, out Func<float, float> add);
                var keys = new Keyframe[frames];
                for (int f = 0; f < frames; f++)
                {
                    float t = f / FrameRate;
                    keys[f] = new Keyframe(t, curve.Evaluate(t % length) + (add != null ? add(t) : 0f));
                }
                AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(keys));
                if (add != null) pending.Remove(b.propertyName);
            }
            foreach (var m in pending)   // a muscle the source never moves starts at 0
            {
                var keys = new Keyframe[frames];
                for (int f = 0; f < frames; f++) { float t = f / FrameRate; keys[f] = new Keyframe(t, m.Value(t)); }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), m.Key), new AnimationCurve(keys));
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // 0..1 rising then falling: a move that starts and ends at rest.
        internal static float Swell(float t, float seconds) => Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI);

        static int EulerAxis(string property) =>
            property.EndsWith(".x") && property.StartsWith("localEulerAngles") ? 0 :
            property.EndsWith(".y") && property.StartsWith("localEulerAngles") ? 1 :
            property.EndsWith(".z") && property.StartsWith("localEulerAngles") ? 2 : -1;

        static int Axis(string property) =>
            property == "m_LocalRotation.x" ? 0 : property == "m_LocalRotation.y" ? 1 : property == "m_LocalRotation.z" ? 2 : property == "m_LocalRotation.w" ? 3 : -1;

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { Transform f = Find(c, name); if (f != null) return f; }
            return null;
        }
    }
}
