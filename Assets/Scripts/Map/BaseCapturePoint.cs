using UnityEngine;

public enum Team { Neutral, Player, Enemy }

[RequireComponent(typeof(MinimapMarker))]
public class BaseCapturePoint : MonoBehaviour
{
    public Team owner = Team.Neutral;
    MinimapMarker marker;

    void Awake()
    {
        marker = GetComponent<MinimapMarker>();
        ApplyColor();
    }

    public void SetOwner(Team team)
    {
        owner = team;
        ApplyColor();
    }

    void ApplyColor()
    {
        marker.color = owner == Team.Player ? new Color(0.2f, 0.5f, 1f)
                     : owner == Team.Enemy ? new Color(1f, 0.25f, 0.25f)
                     : Color.gray;
        // MinimapIcon.Tick reads marker.color every frame, so no need for additional notifications
    }
}