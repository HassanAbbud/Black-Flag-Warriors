using UnityEngine;

public enum MarkerType { Player, Ally, Enemy, Neutral, Base, Objective, Boss}

public class MinimapMarker : MonoBehaviour
{
    public MarkerType type = MarkerType.Enemy;
    public Color color = Color.white;

    [Tooltip("Clamp to edge if outside view bounds)")]
    public bool clampToEdge = false;

    [Tooltip("Icon rotates with target")]
    public bool rotateWithTarget = false;

    public bool InBattle { get; private set; }
    public void SetBattle(bool value) => InBattle = value;

    void OnEnable() => MinimapController.Register(this);
    void OnDisable() => MinimapController.Unregister(this);
}