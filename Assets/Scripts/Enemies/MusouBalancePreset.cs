#if UNITY_EDITOR
using System.IO;
using System.Text;
using Ahoy.Core;
using UnityEditor;
using UnityEngine;

namespace Ahoy.Enemies
{
    // One click to put the musou balance numbers into the enemy assets: Ahoy > Enemies >
    // Apply musou balance preset. It edits assets by name, creates the one missing combo, and
    // logs what it did. The numbers live in this one table so a designer can read and change
    // them; the assets stay the single source of truth once applied. Revert with version control.
    public static class MusouBalancePreset
    {
        [MenuItem("Ahoy/Enemies/Apply musou balance preset")]
        static void Apply()
        {
            var log = new StringBuilder("Musou balance preset:\n");
            int missing = 0;

            // ── Crowd: a fodder imp. Dies to one light hit, bites for 0.8% of Zahim's health,
            // and never staggers him, so a pile of imps is a nuisance, not a death sentence.
            var imp = Find<MonsterDef>("MON_Imp", log, ref missing);
            if (imp != null)
            {
                imp.maxHealth = 50f;
                imp.moveSpeed = 3.2f;
                imp.turnSpeed = 360f;
                imp.stopDistance = 1.2f;
                imp.aggroRange = 40f;
                imp.bodyRadius = 0.5f;
                imp.minReaction = HitReaction.Flinch;
                imp.flinchSeconds = 0.4f;
                imp.knockbackSeconds = 0.6f;
                imp.knockbackDistance = 2.5f;
                imp.blowawayDistance = 5f;
                imp.corpseSeconds = 1f;
                Done(imp, log);
            }

            var bite = Find<MonsterAttackDef>("ATK_Imp_Bite", log, ref missing);
            if (bite != null)
            {
                bite.damage = 8f;
                bite.reaction = HitReaction.None;
                bite.hitstopFrames = 0;
                bite.unblockable = false;
                bite.telegraphSeconds = 0.55f;
                bite.telegraphTurnSpeed = 0f;
                bite.telegraphAimLockAt = 0.6f;
                bite.strikeSeconds = 0.15f;
                bite.recoverSeconds = 0.8f;
                bite.hitboxCenter = new Vector3(0f, 0.9f, 0.8f);
                bite.hitboxHalfExtents = new Vector3(0.6f, 0.9f, 0.7f);
                bite.minRange = 0f;
                bite.maxRange = 1.5f;
                bite.cooldownSeconds = 3.5f;
                bite.lungeDistance = 0.5f;
                bite.lungeSeconds = 0.1f;
                Done(bite, log);
            }

            // ── Boss: a guardian. Slow to turn, readable, heavy hits that Zahim can dodge.
            var sweep = Find<MonsterAttackDef>("ATK_Golem_Sweep", log, ref missing);
            if (sweep != null)
            {
                sweep.damage = 90f;
                sweep.reaction = HitReaction.Knockback;
                sweep.hitstopFrames = 6;
                sweep.unblockable = false;
                sweep.telegraphSeconds = 0.9f;
                sweep.telegraphTurnSpeed = 90f;
                sweep.telegraphAimLockAt = 0.6f;
                sweep.strikeSeconds = 0.3f;
                sweep.recoverSeconds = 0.9f;
                sweep.hitboxCenter = new Vector3(0f, 1f, 2.5f);
                sweep.hitboxHalfExtents = new Vector3(3f, 1.5f, 2.5f);
                sweep.lungeDistance = 0f;
                Done(sweep, log);
            }

            var slam = Find<MonsterAttackDef>("ATK_Golem_Slam", log, ref missing);
            if (slam != null)
            {
                slam.damage = 160f;
                slam.reaction = HitReaction.Launch;
                slam.hitstopFrames = 8;
                slam.unblockable = true;   // R33: the opening attack cannot be guarded, only dodged
                slam.telegraphSeconds = 1.4f;
                slam.telegraphTurnSpeed = 90f;
                slam.telegraphAimLockAt = 0.55f;
                slam.strikeSeconds = 0.25f;
                slam.recoverSeconds = 1f;
                slam.hitboxCenter = new Vector3(0f, 1f, 3f);
                slam.hitboxHalfExtents = new Vector3(1.8f, 1.5f, 2.5f);
                slam.lungeDistance = 0f;
                Done(slam, log);
            }

            var opening = Find<BossPatternDef>("PTN_Golem_Opening", log, ref missing);
            BossPatternDef swipe = null;
            if (opening != null)
            {
                opening.steps = new[] { slam };
                opening.minRange = 0f;
                opening.maxRange = 6f;
                opening.weight = 1f;
                opening.cooldownSeconds = 10f;
                opening.leavesPunishWindow = true;   // R20c: stands open after the crush
                opening.punishSeconds = 2.5f;
                Done(opening, log);

                swipe = Find<BossPatternDef>("PTN_Golem_Swipe", log, ref missing, quiet: true);
                if (swipe == null)
                {
                    swipe = ScriptableObject.CreateInstance<BossPatternDef>();
                    string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(opening));
                    AssetDatabase.CreateAsset(swipe, Path.Combine(folder, "PTN_Golem_Swipe.asset").Replace('\\', '/'));
                    log.AppendLine("  created PTN_Golem_Swipe");
                }
                swipe.steps = new[] { sweep };
                swipe.minRange = 0f;
                swipe.maxRange = 6f;
                swipe.weight = 3f;
                swipe.cooldownSeconds = 2.5f;
                swipe.leavesPunishWindow = false;
                Done(swipe, log);
            }

            var golem = Find<BossDef>("MON_Golem", log, ref missing);
            if (golem != null)
            {
                golem.tier = Tier.Guardian;
                golem.maxHealth = 3000f;
                golem.moveSpeed = 2.2f;
                golem.turnSpeed = 90f;
                golem.stopDistance = 3f;
                golem.aggroRange = 60f;
                golem.bodyRadius = 1.5f;
                golem.restSeconds = 1.5f;
                golem.attackGapSeconds = 1.2f;
                golem.openingPattern = opening;
                golem.patterns = swipe != null ? new[] { opening, swipe } : new[] { opening };
                Done(golem, log);
            }

            AssetDatabase.SaveAssets();
            log.AppendLine(missing == 0 ? "All assets found." : $"{missing} asset(s) not found; those were skipped.");
            Debug.Log(log.ToString());
        }

        static T Find<T>(string assetName, StringBuilder log, ref int missing, bool quiet = false) where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == assetName)
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            if (!quiet)
            {
                missing++;
                log.AppendLine($"  NOT FOUND: {assetName} ({typeof(T).Name})");
            }
            return null;
        }

        static void Done(Object asset, StringBuilder log)
        {
            EditorUtility.SetDirty(asset);
            log.AppendLine($"  set {asset.name}");
        }
    }
}
#endif
