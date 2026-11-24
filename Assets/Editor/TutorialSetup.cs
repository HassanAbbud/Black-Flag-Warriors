using Ahoy.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ahoy.Tools
{
    // Builds the tutorial. Run Tools > Ahoy > Set Up Tutorial with the level scene open.
    // Safe to run again: it only adds what is missing and never overwrites edited text.
    //   1. Assets/Data/Tutorial/TUT_Island.asset with the six pages.
    //   2. A "[Tutorial]" overlay canvas in the active scene: a see-through card in the middle of
    //      the screen, TutorialPanel and TutorialDirector, wired to the asset and Zahim's PlayerInput.
    //   3. A TutorialQuestCue beside every QuestGiver in the scene, wired to the director. The quest
    //      scripts themselves are left untouched.
    static class TutorialSetup
    {
        const string Folder = "Assets/Data/Tutorial";
        const string DefPath = Folder + "/TUT_Island.asset";
        const string RootName = "[Tutorial]";

        static readonly Color CardColor = new Color(0.03f, 0.025f, 0.02f, 0.9f);   // alpha: how see-through
        static readonly Color TitleColor = new Color(1f, 0.82f, 0.4f);
        static readonly Color BodyColor = new Color(0.95f, 0.92f, 0.85f);
        static readonly Color PromptColor = new Color(1f, 1f, 1f, 0.6f);

        [MenuItem("Tools/Ahoy/Set Up Tutorial")]
        static void Setup()
        {
            TutorialDef def = LoadOrCreateDef();
            var scene = EditorSceneManager.GetActiveScene();

            TutorialDirector director = null;
            foreach (GameObject top in scene.GetRootGameObjects())
                if (top.name == RootName) director = top.GetComponent<TutorialDirector>();

            bool built = director == null;
            if (built) director = BuildCanvas(def);
            int cues = AddQuestCues(director);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = director.gameObject;
            Debug.Log($"Tutorial {(built ? "added to" : "already in")} {scene.name}; {cues} quest cue(s) added. " +
                      $"Save the scene to keep it. Edit the words in {DefPath}.");
        }

        static TutorialDef LoadOrCreateDef()
        {
            var def = AssetDatabase.LoadAssetAtPath<TutorialDef>(DefPath);
            if (def != null) return def;

            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Tutorial");

            def = ScriptableObject.CreateInstance<TutorialDef>();
            def.pages = DefaultPages();
            AssetDatabase.CreateAsset(def, DefPath);
            AssetDatabase.SaveAssets();
            return def;
        }

        // The cue must share the QuestGiver's object so it hears the same trigger collider.
        static int AddQuestCues(TutorialDirector director)
        {
            int added = 0;
            foreach (QuestGiver giver in Object.FindObjectsByType<QuestGiver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (giver.GetComponent<TutorialQuestCue>() != null) continue;
                var cue = Undo.AddComponent<TutorialQuestCue>(giver.gameObject);
                var so = new SerializedObject(cue);
                so.FindProperty("director").objectReferenceValue = director;
                so.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }
            if (added == 0 && Object.FindObjectsByType<QuestGiver>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
                Debug.LogWarning("No QuestGiver in this scene; the quest-accepted page will never show.");
            return added;
        }

        static TutorialDirector BuildCanvas(TutorialDef def)
        {
            var rootGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(rootGo, "Set Up Tutorial");
            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // above the HUD and minimap
            var scaler = rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // The card: centred, sized to its text by the layout group.
            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            card.transform.SetParent(rootGo.transform, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(1080f, 0f);
            card.GetComponent<Image>().color = CardColor;
            card.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(64, 64, 44, 36);
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = card.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text title = MakeText(card.transform, "Title", 46f, TitleColor, FontStyles.Bold | FontStyles.SmallCaps, TextAlignmentOptions.Center);
            TMP_Text body = MakeText(card.transform, "Body", 28f, BodyColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 8f;
            TMP_Text prompt = MakeText(card.transform, "Prompt", 24f, PromptColor, FontStyles.Italic, TextAlignmentOptions.Center);

            var panel = rootGo.AddComponent<TutorialPanel>();
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("root").objectReferenceValue = card;
            panelSo.FindProperty("group").objectReferenceValue = card.GetComponent<CanvasGroup>();
            panelSo.FindProperty("title").objectReferenceValue = title;
            panelSo.FindProperty("body").objectReferenceValue = body;
            panelSo.FindProperty("prompt").objectReferenceValue = prompt;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            var director = rootGo.AddComponent<TutorialDirector>();
            var directorSo = new SerializedObject(director);
            directorSo.FindProperty("def").objectReferenceValue = def;
            directorSo.FindProperty("panel").objectReferenceValue = panel;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            PlayerInput input = player != null ? player.GetComponentInChildren<PlayerInput>() : null;
            directorSo.FindProperty("playerInput").objectReferenceValue = input;
            directorSo.ApplyModifiedPropertiesWithoutUndo();
            if (input == null) Debug.LogWarning("No PlayerInput under the Player-tagged object; the director will look again at runtime.", director);

            card.SetActive(false);
            return director;
        }

        static TMP_Text MakeText(Transform parent, string name, float size, Color color, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        // ── The words ────────────────────────────────────────────────────────────────────
        // Written from how the systems really behave: PlayerMotor (auto-sprint, dodge i-frames,
        // dodge-sprint), PortalCamp (wakes in range, meter from its own monsters, boss, seal),
        // BattleSimulation (allies march to help, undefended camps reopen) and WeakPointGauge.
        const string Key = "<color=#FFD27A><b>";
        const string End = "</b></color>";

        static TutorialDef.Page[] DefaultPages() => new[]
        {
            new TutorialDef.Page
            {
                trigger = TutorialTrigger.GameStart,
                title = "The Tide-Born Awakens",
                body =
                    "The sea has carried you back, Zahim, to an island torn open by the portals of the Deep. " +
                    "Before you raise your blade against them, remember the old ways of war.\n\n" +
                    $"{Key}Walk the land{End} — W A S D or the Arrow Keys  ·  Left Stick\n" +
                    $"{Key}Swift strike{End} — Left Mouse or J  ·  Gamepad X\n" +
                    $"{Key}Mighty strike{End} — Right Mouse or K  ·  Gamepad Y\n" +
                    "Weave mighty strikes into a chain of swift ones, and each link of the chain births a different finishing blow.\n" +
                    $"{Key}Dodge{End} — Space  ·  Gamepad A\n" +
                    "In the first heartbeat of a roll no claw or blade can touch you. Keep Dodge held as you rise and the roll becomes a sprint.\n\n" +
                    $"{Key}The wind at your back{End} — run without pause and your stride breaks into a sprint on its own. " +
                    "Halt, and the wind falls still.",
            },
            new TutorialDef.Page
            {
                trigger = TutorialTrigger.GameStart,
                title = "The Village's Plea",
                body =
                    "The village yet stands, but barely. One of its people waits for you among the huts, bearing the island's plea.\n\n" +
                    $"{Key}Walk up to the villager{End} to hear their call and take up the quest. " +
                    "Its charge will be carved upon the quest scroll at the edge of your sight.",
            },
            new TutorialDef.Page
            {
                trigger = TutorialTrigger.QuestAccepted,
                title = "Wounds in the World",
                body =
                    "Swirling portals scar this island. Each lies quiet until you draw near — then it wakes, and the spawn of the Deep pours forth, " +
                    "again and again, for as long as it stays open.\n\n" +
                    "You do not stand alone. The village sends its warriors marching from the lands it holds, to clash with the horde beside you.\n\n" +
                    $"Every monster born of a portal that you fell {Key}fills that portal's meter{End}. " +
                    $"When the meter is full, the portal's {Key}guardian{End} tears its way into the world.",
            },
            new TutorialDef.Page
            {
                trigger = TutorialTrigger.BossSpawned,
                title = "The Guardian Rises",
                body =
                    "The portal shudders, and its guardian steps through. Its kin will keep spilling out while it lives.\n\n" +
                    $"{Key}Slay the guardian and the portal will close{End}, and every last creature it summoned will fade back into the dark.\n\n" +
                    "Watch it well: each blow it readies is foretold. It follows you as it gathers its strength, then commits — " +
                    "dodge late, and let the strike fall upon empty air.",
            },
            new TutorialDef.Page
            {
                trigger = TutorialTrigger.WeakPointRevealed,
                title = "The Crimson Sigil",
                body =
                    $"A {Key}red diamond{End} burns above the guardian. It shows while the beast winds up certain attacks, " +
                    "and while it stands open after finishing a combo.\n\n" +
                    $"{Key}Strike it now!{End} Your blow breaks the attack, and the sigil opens into a gauge. " +
                    "For a few precious breaths the guardian reels, staggered and helpless.\n\n" +
                    $"Every one of your strikes drains the gauge. Empty it, and your next blow is the {Key}Weak Point Smash{End} — " +
                    "enough to fell a guardian outright, and to cleave a great share from a giant.\n\n" +
                    "If the moment passes, the sigil remembers your wounds for a short while. Only your hand can drain it; your allies cannot.",
            },
            new TutorialDef.Page
            {
                trigger = TutorialTrigger.BossDefeated,
                title = "The Portal Is Sealed",
                body =
                    $"The guardian falls, and the portal dims to grey. {Key}This camp is yours now{End}, held in the name of the village.\n\n" +
                    "But the island is not yet saved. Other portals still bleed monsters into the land, and a camp left undefended " +
                    "can be overrun and torn open once more.\n\n" +
                    $"Journey across the island, {Key}seal every portal{End}, and break the Deep's grip on these shores for good.",
            },
        };
    }
}
