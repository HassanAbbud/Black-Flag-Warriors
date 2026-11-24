using System.Collections.Generic;
using Ahoy.Bosses;
using Ahoy.Core;
using Ahoy.Enemies;
using Ahoy.Ships;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ahoy.Tools
{
    // W7.1 additional bosses, land and sea, all from assets already in the project. Run
    // Tools > Ahoy > Set Up Dragon & Sea Bosses; running it again rebuilds everything.
    //   Land: Usurper (South Spit), Nightmare (North-East portal), Terror Bringer (the Great Breach),
    //   built like the Golem (GolemBossSetup): boss controller, weak-point gauge and HUD, minimap marker.
    //   Sea:  Soul Eater, a flying dragon fought from the ship (SeaDragonBoss), and a boss warship
    //   (the enemy ship, bigger and better armed), each sealing its sea portal when sunk.
    static class DragonBossSetup
    {
        const string DragonDir = "Assets/Models/Monsters/Dragons";
        const string DataDir = "Assets/Data/Enemies/Bosses/Dragons";
        const string PrefabDir = "Assets/Prefabs/Enemy/Bosses";
        const string AnimDir = "Assets/Animations/Monsters";
        const float ExitTime = 0.95f;
        const float ExitBlendSeconds = 0.2f;

        // One land dragon: model, clips (file names in its animation folder), size, and stats.
        struct Land
        {
            public string name, model, anim, idle, walk, run, bite, claw, special, hit, die, roar, idle2, defend;
            public bool specialIsFlame;   // flame breath; otherwise a charging horn attack
            public float height, health;
            public Tier tier;
        }

        static readonly Land[] Lands =
        {
            new Land { name = "Usurper", model = "DragonUsurper/Red", anim = "DragonUsurper", idle = "idle01", walk = "Walk", run = "Run",
                       bite = "attackMouth", claw = "attackHand", special = "attackFlame", specialIsFlame = true, hit = "getHit", die = "Die",
                       roar = "Scream", idle2 = "idle02", defend = "Defend",
                       height = 4.5f, health = 2500f, tier = Tier.Guardian },
            new Land { name = "Nightmare", model = "DragonNightmare/DarkBlue", anim = "DragonNightMare", idle = "idle01", walk = "walk", run = "run",
                       bite = "Basic Attack", claw = "Claw Attack", special = "Horn Attack", specialIsFlame = false, hit = "getHit", die = "die",
                       roar = "scream", idle2 = "idle02", defend = "defend",
                       height = 4.5f, health = 3000f, tier = Tier.Guardian },
            new Land { name = "TerrorBringer", model = "DragonTerrorBringer/Red", anim = "DragonTerrorBringer", idle = "idle01", walk = "walk", run = "Run",
                       bite = "Basic Attack", claw = "AttackWingClaw", special = "Flame Attack", specialIsFlame = true, hit = "GetHit", die = "die",
                       roar = "scream", idle2 = "idle02", defend = "Defend",
                       height = 7f, health = 5000f, tier = Tier.Giant },
        };

        [MenuItem("Tools/Ahoy/Set Up Dragon & Sea Bosses")]
        public static void Setup()
        {
            Folder("Assets/Data/Enemies/Bosses", "Dragons");
            Folder("Assets/Prefabs/Enemy", "Bosses");
            var golem = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Golem_Setup.prefab");
            var golemDef = AssetDatabase.LoadAssetAtPath<BossDef>("Assets/Data/Enemies/Bosses/MON_Golem.asset");
            Object balance = new SerializedObject(golem.GetComponentInChildren<BossController>()).FindProperty("balance").objectReferenceValue;
            var marker = golem.GetComponentInChildren<MinimapMarker>();

            GolemBossSetup.Setup();   // the Golem's roar and flinch live in its own tool
            foreach (Land land in Lands) BuildLand(land, golemDef, balance, marker);
            BuildOrcPresence();
            BuildSeaDragon(marker);
            BuildWarship(marker);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bosses] dragons, sea dragon and boss warship built");
        }

        // ── Land dragons ───────────────────────────────────────────────────────
        static void BuildLand(Land land, BossDef golemDef, Object balance, MinimapMarker marker)
        {
            AnimationClip idle = Clip(land.anim, land.idle), walk = Clip(land.anim, land.walk), run = Clip(land.anim, land.run);
            AnimationClip bite = Clip(land.anim, land.bite), claw = Clip(land.anim, land.claw), special = Clip(land.anim, land.special);
            AnimationClip hit = Clip(land.anim, land.hit), die = Clip(land.anim, land.die);
            AnimationClip roar = Clip(land.anim, land.roar), idle2 = Clip(land.anim, land.idle2), defend = Clip(land.anim, land.defend);
            float size = land.height / 4.5f;   // attack reach grows with the dragon

            // Attacks: a quick bite, a wide claw, and the signature move (flame breath or horn charge).
            var biteAtk = Attack($"ATK_{land.name}_Bite", "Bite", bite, 0.7f, 0.25f, 0.7f, 90f, HitReaction.Knockback, false, true,
                                 new Vector3(0f, 1.2f, 3f) * size, new Vector3(1.4f, 1.5f, 1.8f) * size, 0f, 3.5f * size, 1.5f, 0f);
            var clawAtk = Attack($"ATK_{land.name}_Claw", "Claw", claw, 1f, 0.3f, 0.9f, 120f, HitReaction.Knockback, false, true,
                                 new Vector3(0f, 1f, 2.8f) * size, new Vector3(3f, 1.5f, 2.4f) * size, 0f, 4.5f * size, 2f, 0f);
            MonsterAttackDef specialAtk = land.specialIsFlame
                ? Attack($"ATK_{land.name}_Flame", "Special", special, 1.2f, 0.9f, 1f, 140f, HitReaction.Blowaway, true, true,
                         new Vector3(0f, 1f, 7f) * size, new Vector3(2.2f, 2f, 6f) * size, 3f * size, 12f * size, 3f, 0f)
                : Attack($"ATK_{land.name}_Horn", "Special", special, 0.9f, 0.4f, 1f, 130f, HitReaction.Blowaway, true, true,
                         new Vector3(0f, 1f, 2.5f) * size, new Vector3(1.8f, 1.5f, 2f) * size, 4f * size, 14f * size, 3f, 9f * size);

            var pBite = Pattern($"PTN_{land.name}_Bite", biteAtk, 0f, 4f * size, 3f, 1.5f, false);
            var pClaw = Pattern($"PTN_{land.name}_Claw", clawAtk, 0f, 5f * size, 2f, 3f, true);
            var pSpecial = Pattern($"PTN_{land.name}_Special", specialAtk, land.specialIsFlame ? 3f * size : 4f * size, 14f * size, 2f, 5f, true);

            string defPath = $"{DataDir}/MON_{land.name}.asset";
            var def = AssetDatabase.LoadAssetAtPath<BossDef>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BossDef>();
                EditorUtility.CopySerialized(golemDef, def);
                def.name = $"MON_{land.name}";
                AssetDatabase.CreateAsset(def, defPath);
            }
            def.tier = land.tier;
            def.maxHealth = land.health;
            def.moveSpeed = 3.2f;
            def.turnSpeed = 110f;
            def.stopDistance = 2.5f * size;
            def.aggroRange = 60f;
            def.bodyRadius = 1.4f * size;
            def.openingPattern = pSpecial;
            def.patterns = new[] { pBite, pClaw, pSpecial };
            var sdef = new SerializedObject(def);
            sdef.FindProperty("staggerClip").objectReferenceValue = hit;
            sdef.ApplyModifiedPropertiesWithoutUndo();
            def.roarSeconds = roar != null ? roar.length : 0f;
            def.hitFlinchCooldown = 2f;
            def.fidgetEvery = new Vector2(4f, 9f);
            EditorUtility.SetDirty(def);

            AnimatorController controller = LandController(land.name, idle, walk, run, bite, claw, special, hit, die, def.moveSpeed);
            AddPresence(controller, roar, hit, idle2, defend);

            // Prefab: a boss root (controller, gauge, HUD, marker) over the dragon model scaled to size.
            var root = new GameObject($"DRG_{land.name}");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{DragonDir}/Prefab/{land.model}.prefab"));
            model.transform.SetParent(root.transform, false);
            float scale = land.height / Mathf.Max(0.01f, Bounds(model).size.y);
            model.transform.localScale = Vector3.one * scale;
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            Bounds bounds = Bounds(model);
            var body = root.AddComponent<CharacterController>();
            body.height = land.height * 0.8f;
            body.radius = Mathf.Min(body.height * 0.5f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.45f);
            body.center = new Vector3(0f, body.height * 0.5f, 0f);

            var boss = root.AddComponent<BossController>();
            var sb = new SerializedObject(boss);
            sb.FindProperty("def").objectReferenceValue = def;
            sb.FindProperty("balance").objectReferenceValue = balance;
            sb.FindProperty("controller").objectReferenceValue = body;
            sb.FindProperty("animator").objectReferenceValue = animator;
            sb.ApplyModifiedPropertiesWithoutUndo();

            var gauge = root.AddComponent<WeakPointGauge>();
            var sg = new SerializedObject(gauge);
            sg.FindProperty("balance").objectReferenceValue = balance;
            sg.FindProperty("boss").objectReferenceValue = boss;
            sg.ApplyModifiedPropertiesWithoutUndo();
            GolemBossSetup.BuildHud(root.transform, land.height + 0.7f, balance);
            CopyMarker(marker, root);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/DRG_{land.name}.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[Bosses] DRG_{land.name}: {land.height} m, model scale {scale:F2}, {land.health} HP, {land.tier}");
        }

        static AnimatorController LandController(string name, AnimationClip idle, AnimationClip walk, AnimationClip run,
                                                 AnimationClip bite, AnimationClip claw, AnimationClip special,
                                                 AnimationClip hit, AnimationClip die, float runSpeed)
        {
            string path = $"{AnimDir}/AC_Dragon_{name}.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(One("AttackSpeed"));
            ac.AddParameter(One("StunSpeed"));
            var machine = ac.layers[0].stateMachine;
            AnimatorState locomotion = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, runSpeed * 0.5f);
            tree.AddChild(run, runSpeed);
            machine.defaultState = locomotion;
            AddAttack(machine, "Bite", bite, locomotion);
            AddAttack(machine, "Claw", claw, locomotion);
            AddAttack(machine, "Special", special, locomotion);
            AnimatorState stagger = machine.AddState("Stagger");
            stagger.motion = hit;
            stagger.speedParameter = "StunSpeed";
            stagger.speedParameterActive = true;
            machine.AddState("Death").motion = die;
            EditorUtility.SetDirty(ac);
            return ac;
        }

        // Roar, Hit, Fidget and Fidget2: one-shots that hand back to Locomotion (BossController plays them).
        internal static void AddPresence(AnimatorController ac, AnimationClip roar, AnimationClip hit, AnimationClip fidget, AnimationClip fidget2)
        {
            var machine = ac.layers[0].stateMachine;
            AnimatorState locomotion = machine.defaultState;
            foreach (var (name, clip) in new[] { ("Roar", roar), ("Hit", hit), ("Fidget", fidget), ("Fidget2", fidget2) })
            {
                if (clip == null) continue;
                AnimatorState state = machine.AddState(name);
                state.motion = clip;
                AnimatorStateTransition back = state.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = ExitTime;
                back.hasFixedDuration = true;
                back.duration = ExitBlendSeconds;
            }
            EditorUtility.SetDirty(ac);
        }

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

        static MonsterAttackDef Attack(string file, string state, AnimationClip clip, float telegraph, float strike, float recover,
                                       float damage, HitReaction reaction, bool unblockable, bool counterWindow,
                                       Vector3 center, Vector3 half, float minRange, float maxRange, float cooldown, float lunge)
        {
            string path = $"{DataDir}/{file}.asset";
            var a = AssetDatabase.LoadAssetAtPath<MonsterAttackDef>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<MonsterAttackDef>(); AssetDatabase.CreateAsset(a, path); }
            a.animatorState = state;
            // The whole clip plays across telegraph, strike and recovery.
            a.playbackSpeed = clip != null ? Mathf.Clamp(clip.length / (telegraph + strike + recover), 0.6f, 1.6f) : 1f;
            a.crossFadeSeconds = 0.1f;
            a.telegraphSeconds = telegraph;
            a.telegraphTurnSpeed = 60f;
            a.telegraphAimLockAt = 0.6f;
            a.opensCounterWindow = counterWindow;
            a.strikeSeconds = strike;
            a.recoverSeconds = recover;
            a.damage = damage;
            a.reaction = reaction;
            a.hitstopFrames = 6;
            a.unblockable = unblockable;
            a.hitboxCenter = center;
            a.hitboxHalfExtents = half;
            a.minRange = minRange;
            a.maxRange = maxRange;
            a.cooldownSeconds = cooldown;
            a.lungeDistance = lunge;
            a.lungeSeconds = lunge > 0f ? strike : 0.15f;
            EditorUtility.SetDirty(a);
            return a;
        }

        static BossPatternDef Pattern(string file, MonsterAttackDef step, float minRange, float maxRange, float weight, float cooldown, bool punish)
        {
            string path = $"{DataDir}/{file}.asset";
            var p = AssetDatabase.LoadAssetAtPath<BossPatternDef>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<BossPatternDef>(); AssetDatabase.CreateAsset(p, path); }
            p.steps = new[] { step };
            p.minRange = minRange;
            p.maxRange = maxRange;
            p.weight = weight;
            p.cooldownSeconds = cooldown;
            p.leavesPunishWindow = punish;
            p.punishSeconds = 2.5f;
            EditorUtility.SetDirty(p);
            return p;
        }

        // ── Orc captain: the pack has no hit or roar, so both are made from its combat idle ──
        static void BuildOrcPresence()
        {
            const string dir = "Assets/Models/Monsters/Orc_Wolf";
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/prefab/Orc_Wolfrider.prefab");
            AnimationClip idle = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath($"{dir}/animation/Orc_wolfrider_05_combat_idle.FBX"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) idle = c;
            const float hitSeconds = 0.5f, roarSeconds = 1.6f;
            // Hit: the rider snaps back from the blow. Roar: rider and wolf lift their heads and bellow.
            var hit = BossClipBaker.Bake(idle, rig, $"{AnimDir}/Orc_Hit.anim", hitSeconds,
                new BossClipBaker.Turn { bone = "Bip001 Spine", euler = t => new Vector3(-22f * BossClipBaker.Swell(t, hitSeconds), 0f, 0f) },
                new BossClipBaker.Turn { bone = "Bip001 Head", euler = t => new Vector3(-15f * BossClipBaker.Swell(t, hitSeconds), 0f, 0f) });
            var roar = BossClipBaker.Bake(idle, rig, $"{AnimDir}/Orc_Roar.anim", roarSeconds,
                new BossClipBaker.Turn { bone = "Bip001 Spine", euler = t => new Vector3(-15f * BossClipBaker.Swell(t, roarSeconds), 0f, 0f) },
                new BossClipBaker.Turn { bone = "Bip001 Head", euler = t => new Vector3(-35f * BossClipBaker.Swell(t, roarSeconds), 0f, 0f) },
                new BossClipBaker.Turn { bone = "Bip001 Head001", euler = t => new Vector3(-35f * BossClipBaker.Swell(t, roarSeconds), 0f, 0f) });

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{AnimDir}/AC_OrcCaptain.controller");
            var machine = ac.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == "HitFlinch" || child.state.name == "HitKnockback") child.state.motion = hit;
                if (child.state.name == "Roar") machine.RemoveState(child.state);
            }
            AnimatorState roarState = machine.AddState("Roar");
            roarState.motion = roar;
            AnimatorStateTransition back = roarState.AddTransition(machine.defaultState);
            back.hasExitTime = true; back.exitTime = ExitTime; back.hasFixedDuration = true; back.duration = ExitBlendSeconds;
            EditorUtility.SetDirty(ac);

            var def = AssetDatabase.LoadAssetAtPath<MonsterDef>("Assets/Data/Enemies/Captains/MON_OrcCaptain.asset");
            def.roarSeconds = roarSeconds;
            EditorUtility.SetDirty(def);
            Debug.Log("[Bosses] orc captain: hit and roar clips baked");
        }

        // ── Sea dragon ─────────────────────────────────────────────────────────
        static void BuildSeaDragon(MinimapMarker marker)
        {
            const string anim = "DragonSoulEater";
            string acPath = $"{AnimDir}/AC_SeaDragon_SoulEater.controller";
            AssetDatabase.DeleteAsset(acPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(acPath);
            var machine = ac.layers[0].stateMachine;
            AnimatorState fly = machine.AddState("Fly");
            fly.motion = Clip(anim, "Fly Float");
            machine.defaultState = fly;
            AnimatorState shoot = machine.AddState("Shoot");
            shoot.motion = Clip(anim, "Fly Fireball Shoot");
            AnimatorStateTransition back = shoot.AddTransition(fly);
            back.hasExitTime = true; back.exitTime = ExitTime; back.hasFixedDuration = true; back.duration = ExitBlendSeconds;
            machine.AddState("Glide").motion = Clip(anim, "Fly Glide");   // its fall after the killing shot
            machine.AddState("Die").motion = Clip(anim, "Die");

            var def = Asset<SeaDragonDef>($"{DataDir}/SEA_SoulEater.asset");

            // Fireballs: the enemy cannonball pool with a glowing ball, a lighter arc and more in flight.
            var cannonDef = AssetDatabase.LoadAssetAtPath<CannonballDef>("Assets/Data/Ships/PRJ_Cannonball.asset");
            string fireDefPath = $"{DataDir}/PRJ_Fireball.asset";
            var fireDef = AssetDatabase.LoadAssetAtPath<CannonballDef>(fireDefPath);
            if (fireDef == null) { fireDef = ScriptableObject.CreateInstance<CannonballDef>(); EditorUtility.CopySerialized(cannonDef, fireDef); fireDef.name = "PRJ_Fireball"; AssetDatabase.CreateAsset(fireDef, fireDefPath); }
            fireDef.poolSize = 96;
            fireDef.radius = 0.7f;
            fireDef.gravity = 4f;
            fireDef.lifetime = 7f;
            EditorUtility.SetDirty(fireDef);
            GameObject fireball = FireballPrefab();

            var root = new GameObject("SEA_SoulEater");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{DragonDir}/Prefab/DragonSoulEater/Grey.prefab"));
            model.transform.SetParent(root.transform, false);
            float height = 6f;
            model.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, Bounds(model).size.y));
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = ac;
            animator.applyRootMotion = false;
            Bounds bounds = Bounds(model);
            var hitbox = root.AddComponent<SphereCollider>();
            hitbox.center = root.transform.InverseTransformPoint(bounds.center);
            hitbox.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 0.6f;

            Transform head = FindDeep(model.transform, "Head") ?? model.transform;
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(head, false);
            muzzle.position = head.position + root.transform.forward * 1.2f;

            var poolGo = new GameObject("Fireballs");
            poolGo.transform.SetParent(root.transform, false);
            var pool = poolGo.AddComponent<CannonballPool>();
            var sp = new SerializedObject(pool);
            sp.FindProperty("def").objectReferenceValue = fireDef;
            sp.FindProperty("cannonballPrefab").objectReferenceValue = fireball;
            sp.ApplyModifiedPropertiesWithoutUndo();

            var dragon = root.AddComponent<SeaDragonBoss>();
            var sd = new SerializedObject(dragon);
            sd.FindProperty("def").objectReferenceValue = def;
            sd.FindProperty("animator").objectReferenceValue = animator;
            sd.FindProperty("muzzle").objectReferenceValue = muzzle;
            sd.FindProperty("fireballs").objectReferenceValue = pool;
            sd.ApplyModifiedPropertiesWithoutUndo();
            CopyMarker(marker, root);

            Debug.Log($"[Bosses] SEA_SoulEater: {def.maxHealth} HP, hitbox {hitbox.radius:F1} m");
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/SEA_SoulEater.prefab");
            Object.DestroyImmediate(root);
        }

        static GameObject FireballPrefab()
        {
            string matPath = $"{DataDir}/MAT_Fireball.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
                mat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.1f));
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(4f, 1.4f, 0.2f));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "PRJ_Fireball";
            Object.DestroyImmediate(ball.GetComponent<Collider>());   // the pool sweeps for hits itself
            ball.transform.localScale = Vector3.one * 1.4f;
            var r = ball.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(ball, $"{PrefabDir}/PRJ_Fireball.prefab");
            Object.DestroyImmediate(ball);
            return prefab;
        }

        // ── Boss warship ───────────────────────────────────────────────────────
        static void BuildWarship(MinimapMarker marker)
        {
            var baseDef = AssetDatabase.LoadAssetAtPath<EnemyShipDef>("Assets/Data/Ships/MON_EnemyShip.asset");
            string defPath = $"{DataDir}/EDF_BossWarship.asset";
            var def = AssetDatabase.LoadAssetAtPath<EnemyShipDef>(defPath);
            if (def == null) { def = ScriptableObject.CreateInstance<EnemyShipDef>(); EditorUtility.CopySerialized(baseDef, def); def.name = "EDF_BossWarship"; AssetDatabase.CreateAsset(def, defPath); }
            def.detectRange = 100f;
            def.loseRange = 140f;
            def.engageDistance = 45f;
            def.fireCooldown = 1.6f;
            def.cannonStagger = 0.08f;
            def.cannonDamage = 80f;
            def.wanderRadius = 30f;
            EditorUtility.SetDirty(def);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ship/EnemyShipMedium/EnemyShipMedium.prefab");
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(source);
            ship.name = "SEA_BossWarship";
            ship.transform.localScale = Vector3.one * 1.6f;

            var controller = ship.GetComponent<EnemyShipController>();
            var sc = new SerializedObject(controller);
            sc.FindProperty("def").objectReferenceValue = def;
            sc.FindProperty("shipHealth").intValue = 6000;
            sc.ApplyModifiedPropertiesWithoutUndo();

            // Two more cannons a side, along the hull, and its own cannonball pool.
            var cannons = ship.GetComponent<EnemyShipCannons>();
            var scn = new SerializedObject(cannons);
            AddMuzzles(scn.FindProperty("portMuzzles"));
            AddMuzzles(scn.FindProperty("starboardMuzzles"));
            var poolInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ship/EnemyShipMedium/CannonballPool.prefab"));
            poolInstance.transform.SetParent(ship.transform, false);
            scn.FindProperty("pool").objectReferenceValue = poolInstance.GetComponent<CannonballPool>();
            scn.ApplyModifiedPropertiesWithoutUndo();
            if (ship.GetComponent<MinimapMarker>() == null) CopyMarker(marker, ship);
            if (ship.GetComponent<ShipSinking>() == null) ship.AddComponent<ShipSinking>();

            PrefabUtility.SaveAsPrefabAsset(ship, $"{PrefabDir}/SEA_BossWarship.prefab");
            Object.DestroyImmediate(ship);
            Debug.Log("[Bosses] SEA_BossWarship: 6000 HP, 3 cannons a side");
        }

        static void AddMuzzles(SerializedProperty muzzles)
        {
            if (muzzles.arraySize == 0) return;
            var first = (Transform)muzzles.GetArrayElementAtIndex(0).objectReferenceValue;
            int start = muzzles.arraySize;
            for (int k = 0; k < 2; k++)
            {
                var extra = Object.Instantiate(first.gameObject, first.parent).transform;
                extra.name = $"{first.name}_{k + 2}";
                extra.localPosition = first.localPosition + Vector3.forward * (k == 0 ? 2.5f : -2.5f);
                muzzles.arraySize++;
                muzzles.GetArrayElementAtIndex(start + k).objectReferenceValue = extra;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        static void CopyMarker(MinimapMarker marker, GameObject onto)
        {
            if (marker == null) return;
            UnityEditorInternal.ComponentUtility.CopyComponent(marker);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(onto);
        }

        static AnimationClip Clip(string folder, string file)
        {
            string path = $"{DragonDir}/Animations/{folder}/{file}.fbx";
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            Debug.LogError($"[Bosses] no clip in {path}");
            return null;
        }

        static AnimatorControllerParameter One(string name) =>
            new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = 1f };

        static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return b;
        }

        static T Asset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
