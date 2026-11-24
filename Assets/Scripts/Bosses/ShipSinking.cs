using Ahoy.Ships;
using UnityEngine;

namespace Ahoy.Bosses
{
    // W7.1: an enemy ship that dies lists, rolls and goes under while it burns, instead of just
    // switching off. Only listens to EnemyShipController.StateChanged; the ship never knows it exists.
    [RequireComponent(typeof(EnemyShipController))]
    public sealed class ShipSinking : MonoBehaviour
    {
        [SerializeField] float rollDegrees = 35f;
        [SerializeField] float pitchDegrees = 12f;
        [Tooltip("Metres it goes under by the time it is switched off.")]
        [SerializeField] float sinkDepth = 8f;

        EnemyShipController ship;
        Quaternion startRotation;
        Vector3 startPosition;
        float sinkingFor = -1f;

        void Awake() => ship = GetComponent<EnemyShipController>();
        void OnEnable() => ship.StateChanged += OnStateChanged;

        // Back upright for reuse: the spawner may turn it on again.
        void OnDisable()
        {
            ship.StateChanged -= OnStateChanged;
            if (sinkingFor >= 0f) transform.SetPositionAndRotation(startPosition, startRotation);
            sinkingFor = -1f;
        }

        void OnStateChanged(EnemyShipController.ShipState state)
        {
            if (state != EnemyShipController.ShipState.Dead) return;
            startPosition = transform.position;
            startRotation = transform.rotation;
            sinkingFor = 0f;
        }

        void Update()
        {
            if (sinkingFor < 0f) return;
            sinkingFor += Time.deltaTime;
            float t = Mathf.Clamp01(sinkingFor / Mathf.Max(0.1f, ship.Def.burnSeconds));
            float ease = t * t;   // slow at first, then it goes
            transform.SetPositionAndRotation(startPosition + Vector3.down * (sinkDepth * ease),
                                             startRotation * Quaternion.Euler(pitchDegrees * ease, 0f, rollDegrees * t));
        }
    }
}
