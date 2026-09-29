using UnityEngine;
using System.Collections.Generic;

// Rebinds every SkinnedMeshRenderer under this object to another skeleton,
// matching bones by name. Put it on Superhero_Male_Head and set Root Bone
// to Male_Ranger > Armature.
public class BindToSkeleton : MonoBehaviour
{
    [Tooltip("The Armature of the character whose skeleton should drive this mesh (e.g. Male_Ranger > Armature).")]
    public Transform rootBone;

    void Awake()
    {
        if (rootBone == null)
        {
            Debug.LogError($"{name}: BindToSkeleton has no Root Bone assigned.", this);
            return;
        }

        // Map bone name -> bone. The first match wins, so the target's real bones
        // take priority over any duplicate-named bones nested deeper (e.g. a hair
        // model's own armature parented under the Head bone).
        var map = new Dictionary<string, Transform>();
        foreach (var t in rootBone.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map[t.name] = t;

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var bones = smr.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                if (map.TryGetValue(bones[i].name, out var target)) bones[i] = target;
                else Debug.LogWarning($"{smr.name}: no bone named '{bones[i].name}' under {rootBone.name}", this);
            }
            smr.bones = bones;

            if (smr.rootBone != null && map.TryGetValue(smr.rootBone.name, out var newRoot))
                smr.rootBone = newRoot;
        }
    }
}