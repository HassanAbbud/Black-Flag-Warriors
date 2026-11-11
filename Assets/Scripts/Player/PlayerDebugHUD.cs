using UnityEngine;

namespace Ahoy.Player
{
    // Development-only readout of the player state machine; draws nothing in release builds.
    // The real HUD (lock-on reticle included) belongs to Presentation.
    public sealed class PlayerDebugHUD : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAttack attack;
        [SerializeField] PlayerHealth health;
        [SerializeField] LockOn lockOn;
        [SerializeField] bool show = true;

        void OnGUI()
        {
            if (!show || !Debug.isDebugBuild) return;
            GUI.Box(new Rect(10, 10, 260, 90), GUIContent.none);
            GUI.Label(new Rect(20, 15, 240, 20), "State: " + motor.State + (motor.IsInvulnerable ? "  (invulnerable)" : ""));
            GUI.Label(new Rect(20, 35, 240, 20), "Attack: " + (attack.Current != null ? attack.Current.name : "-"));
            GUI.Label(new Rect(20, 55, 240, 20), "Health: " + health.Current.ToString("0") + " / " + health.Max.ToString("0"));
            GUI.Label(new Rect(20, 75, 240, 20), "Lock-on: " + (lockOn != null && lockOn.HasTarget ? lockOn.Target.name : "-"));

            if (lockOn == null || !lockOn.HasTarget || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(lockOn.Target.position + Vector3.up * 2f);
            if (screen.z <= 0f) return;
            GUI.Label(new Rect(screen.x - 30f, Screen.height - screen.y - 10f, 60f, 20f), "[ ◆ ]");
        }
    }
}
