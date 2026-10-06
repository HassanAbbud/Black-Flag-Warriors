using UnityEngine;

namespace Ahoy.Player
{
    // Development-only readout of the player state machine; draws nothing in release builds.
    // The real HUD belongs to Presentation.
    public sealed class PlayerDebugHUD : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAttack attack;
        [SerializeField] PlayerHealth health;
        [SerializeField] bool show = true;

        void OnGUI()
        {
            if (!show || !Debug.isDebugBuild) return;
            GUI.Box(new Rect(10, 10, 260, 70), GUIContent.none);
            GUI.Label(new Rect(20, 15, 240, 20), "State: " + motor.State + (motor.IsInvulnerable ? "  (invulnerable)" : ""));
            GUI.Label(new Rect(20, 35, 240, 20), "Attack: " + (attack.Current != null ? attack.Current.name : "-"));
            GUI.Label(new Rect(20, 55, 240, 20), "Health: " + health.Current.ToString("0") + " / " + health.Max.ToString("0"));
        }
    }
}
