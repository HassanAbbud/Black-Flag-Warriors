using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Island
{
    // W6.1 balance overlay, toggled with F3: every location's holder and both sides' strength, on
    // the field and in reserve, plus morale, the crowd share the frame rate allows, and FPS. For
    // tuning by playing; it reads the simulation and changes nothing.
    public sealed class BattleDebugOverlay : MonoBehaviour
    {
        [SerializeField] BattleSimulation simulation;
        [SerializeField] bool visible;

        GUIStyle style;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;
        }

        // shortcut: OnGUI builds strings every frame, fine for a debug panel that is off by default.
        void OnGUI()
        {
            if (!visible || simulation == null || simulation.Locations == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };

            IBattleProgress battle = simulation;
            ISpawnPacing pacing = BattleContext.Pacing;
            float fps = 1f / Mathf.Max(0.0001f, Time.smoothDeltaTime);
            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 300f, 140f, 600f, 520f), GUI.skin.box);
            GUILayout.Label($"BALANCE (F3)   FPS {fps:0}   Morale {battle.Morale01 * 100f:0}%   Field: allies {battle.FieldAllies} vs monsters {battle.FieldMonsters}" +
                            $"   front intervals M {pacing.FrontMonsterInterval:0.0}s A {pacing.FrontAllyInterval:0.0}s", style);
            GUILayout.Label("Location                    holder     monsters field/pool     allies field/pool", style);
            foreach (BattleLocation l in simulation.Locations)
            {
                int fm = l.front != null ? l.front.FieldMonsters : 0;
                int fa = l.front != null ? l.front.FieldAllies : 0;
                string fight = l.fighting ? "  FIGHT" : "";
                GUILayout.Label($"{l.DisplayName,-26}  {l.holder,-9}  {fm,4} / {l.monsters,6:0.0}        {fa,4} / {l.allies,6:0.0}{fight}", style);
            }
            GUILayout.EndArea();
        }
    }
}
