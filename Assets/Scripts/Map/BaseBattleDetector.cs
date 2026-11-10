using UnityEngine;

[RequireComponent(typeof(MinimapMarker))]
public class BaseBattleDetector : MonoBehaviour
{
    public float radius = 25f;
    public LayerMask playerSideMask;
    public LayerMask enemySideMask;
    public float checkInterval = 0.3f;   
    public float exitDelay = 1.5f;       // avoid flickering when soldiers temporarily leave the area

    MinimapMarker marker;
    float timer;
    float lastBattleTime = -999f;

    void Awake() => marker = GetComponent<MinimapMarker>();

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        Vector3 c = transform.position;
        bool fighting =
            Physics.CheckSphere(c, radius, playerSideMask, QueryTriggerInteraction.Ignore) &&
            Physics.CheckSphere(c, radius, enemySideMask, QueryTriggerInteraction.Ignore);

        if (fighting) lastBattleTime = Time.time;
        marker.SetBattle(Time.time - lastBattleTime < exitDelay);
    }

    // triggered by BaseCapturePoint when the base is attacked
    public void NotifyAttacked() => lastBattleTime = Time.time;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}