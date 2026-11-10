using System;
using Ahoy.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ahoy.Presentation
{
    // W5.6 battle progress bar and counters. Reads only IBattleProgress through BattleContext and
    // listens to BattleEvents.BattleEnded: island reclaimed, portals sealed, village integrity, KO
    // count, battle time, the meter of the location Zahim is at, and where the sides are fighting
    // right now (GDD HUD). Text is refreshed a few times a second with SetText, which does not allocate.
    public sealed class BattleHUD : MonoBehaviour
    {
        [Serializable]
        struct LocationRow
        {
            public GameObject root;
            public Image dot;
            public TMP_Text label;
            public TMP_Text status;
        }

        [SerializeField] float refreshSeconds = 0.25f;

        [Header("Island")]
        [Tooltip("Fill whose anchorMax.x is the share of the island won back.")]
        [SerializeField] RectTransform progressFill;
        [SerializeField] TMP_Text progressLabel;
        [SerializeField] TMP_Text portalsLabel;

        [Header("Village")]
        [SerializeField] RectTransform villageFill;
        [SerializeField] Image villageFillImage;
        [SerializeField] TMP_Text villageLabel;
        [SerializeField] Color villageLowColor = new Color(0.85f, 0.2f, 0.15f);
        [SerializeField] Color villageHighColor = new Color(0.3f, 0.8f, 0.3f);

        [Header("Counters")]
        [SerializeField] TMP_Text koLabel;
        [SerializeField] TMP_Text timeLabel;

        [Header("Where Zahim is")]
        [SerializeField] GameObject herePanel;
        [SerializeField] TMP_Text hereName;
        [SerializeField] RectTransform hereFill;
        [SerializeField] TMP_Text hereStatus;

        [Header("Battle status")]
        [SerializeField] LocationRow[] rows;
        [SerializeField] Color monstersColor = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] Color villageHeldColor = new Color(0.25f, 0.55f, 1f);
        [SerializeField] Color fightingColor = new Color(1f, 0.7f, 0.1f);
        [SerializeField] Color quietColor = new Color(0.8f, 0.8f, 0.8f);

        [Header("End of battle")]
        [SerializeField] GameObject endBanner;
        [SerializeField] TMP_Text endLabel;

        float nextRefreshAt;
        int boundLocations = -1;

        void OnEnable() => BattleEvents.BattleEnded += OnBattleEnded;
        void OnDisable() => BattleEvents.BattleEnded -= OnBattleEnded;

        void Start()
        {
            if (endBanner != null) endBanner.SetActive(false);
            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefreshAt) return;
            nextRefreshAt = Time.unscaledTime + refreshSeconds;
            Refresh();
        }

        void Refresh()
        {
            IBattleProgress battle = BattleContext.Progress;
            if (boundLocations != battle.LocationCount) BindRows(battle);

            SetFill(progressFill, battle.Progress01);
            progressLabel.SetText("ISLAND RECLAIMED  {0:0}/{1:0}", (float)battle.HeldCount, Mathf.Max(0, battle.LocationCount - 1));
            portalsLabel.SetText("Portals sealed {0:0}/{1:0}", (float)battle.SealedCount, (float)battle.PortalCount);

            float integrity = battle.VillageIntegrity01;
            SetFill(villageFill, integrity);
            if (villageFillImage != null) villageFillImage.color = Color.Lerp(villageLowColor, villageHighColor, integrity);
            villageLabel.SetText("VILLAGE  {0:0}%", integrity * 100f);

            koLabel.SetText("KO  {0:0}", (float)battle.KoCount);
            int seconds = Mathf.FloorToInt(battle.BattleSeconds);
            timeLabel.SetText("{0:00}:{1:00}", (float)(seconds / 60), (float)(seconds % 60));

            RefreshHere(battle);
            RefreshRows(battle);
        }

        void RefreshHere(IBattleProgress battle)
        {
            bool here = battle.TryGetLocation(battle.SeenLocationId, out BattleLocationStatus status);
            if (herePanel != null) herePanel.SetActive(here);
            if (!here) return;

            hereName.text = status.Name;   // a cached string from the location, not a new one
            SetFill(hereFill, status.Meter01);
            if (status.Holder == Faction.Village) hereStatus.SetText("Held by the village");
            else if (status.Locked) hereStatus.SetText("Seal more portals to open the breach");
            else if (status.IsPortal) hereStatus.SetText("Portal meter {0:0}%", status.Meter01 * 100f);
            else hereStatus.SetText("Cleared {0:0}%", status.Meter01 * 100f);
        }

        void RefreshRows(IBattleProgress battle)
        {
            for (int i = 0; i < rows.Length && i < battle.LocationCount; i++)
            {
                BattleLocationStatus status = battle.GetLocation(i);
                LocationRow row = rows[i];
                row.dot.color = status.Holder == Faction.Monsters ? monstersColor : villageHeldColor;

                if (status.Fighting)
                {
                    row.status.color = fightingColor;
                    row.status.SetText("FIGHTING");
                }
                else if (status.Locked)
                {
                    row.status.color = quietColor;
                    row.status.SetText("locked");
                }
                else if (status.NextWaveIn >= 0f)
                {
                    row.status.color = quietColor;
                    row.status.SetText("wave in {0:0}s", status.NextWaveIn);
                }
                else
                {
                    row.status.color = quietColor;
                    row.status.SetText(status.Holder == Faction.Village ? "held" : "monsters");
                }
            }
        }

        // Names are written once, when the battle's location list first appears.
        void BindRows(IBattleProgress battle)
        {
            boundLocations = battle.LocationCount;
            for (int i = 0; i < rows.Length; i++)
            {
                bool used = i < battle.LocationCount;
                rows[i].root.SetActive(used);
                if (used) rows[i].label.text = battle.GetLocation(i).Name;
            }
        }

        void OnBattleEnded(bool won)
        {
            if (endBanner == null) return;
            endBanner.SetActive(true);
            endLabel.SetText(won ? "VICTORY" : "THE VILLAGE HAS FALLEN");
        }

        static void SetFill(RectTransform fill, float value)
        {
            if (fill == null) return;
            Vector2 max = fill.anchorMax;
            max.x = Mathf.Clamp01(value);
            fill.anchorMax = max;
        }
    }
}
