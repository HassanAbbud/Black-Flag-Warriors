using Ahoy.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ahoy.Portal
{
    // The camp's trigger circle drawn on the ground in game: dormant, active (red) or sealed (blue).
    // Only listens to PortalCamp.PhaseChanged; the camp never knows it exists.
    // Put it on a child of the portal. Colours, width and ground layers live in PortalCampDef.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PortalRing : MonoBehaviour
    {
        const string FallbackShader = "Sprites/Default";

        [Tooltip("Left empty, the PortalCamp on a parent is used.")]
        [SerializeField] PortalCamp camp;

        LineRenderer line;
        Vector3[] points;
        float builtRadius = -1f;
        Vector3 builtCenter;

        void Awake()
        {
            if (camp == null) camp = GetComponentInParent<PortalCamp>();
            Debug.Assert(camp != null, "PortalRing needs a PortalCamp on itself or a parent", this);

            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            // Lie flat: TransformZ faces the strip along this object's forward, which points up.
            line.alignment = LineAlignment.TransformZ;
            transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (line.sharedMaterial == null)
            {
                Debug.LogWarning($"{name}: no material on the LineRenderer, using {FallbackShader}", this);
                line.sharedMaterial = new Material(Shader.Find(FallbackShader));
            }
        }

        void OnEnable()
        {
            if (camp == null) return;
            camp.PhaseChanged += Paint;
            Paint(camp.Phase);
        }

        void OnDisable()
        {
            if (camp != null) camp.PhaseChanged -= Paint;
        }

        // Rebuilt only when the radius or the portal moves, so the trigger radius can be tuned live.
        void LateUpdate()
        {
            if (camp == null || camp.Def == null) return;
            if (camp.Def.triggerRadius != builtRadius || camp.transform.position != builtCenter) Build();
        }

        void Paint(PortalPhase phase)
        {
            PortalCampDef def = camp.Def;
            if (def == null) return;

            Color color = phase switch
            {
                PortalPhase.Open => def.ringDormantColor,
                PortalPhase.Sealed => def.ringSealedColor,
                _ => def.ringActiveColor,
            };
            line.startColor = color;
            line.endColor = color;
            line.enabled = color.a > 0f;
        }

        // Each point drops onto the ground below it, so the ring follows hills instead of cutting through them.
        void Build()
        {
            PortalCampDef def = camp.Def;
            builtRadius = def.triggerRadius;
            builtCenter = camp.transform.position;

            if (points == null || points.Length != def.ringSegments) points = new Vector3[def.ringSegments];
            float probe = def.ringGroundProbeHeight;
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / points.Length;
                Vector3 point = builtCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * builtRadius;
                if (Physics.Raycast(point + Vector3.up * probe, Vector3.down, out RaycastHit hit, probe * 2f,
                                    def.ringGroundMask, QueryTriggerInteraction.Ignore))
                    point.y = hit.point.y;
                point.y += def.ringGroundOffset;
                points[i] = point;
            }

            line.widthMultiplier = def.ringWidth;
            line.positionCount = points.Length;
            line.SetPositions(points);
        }
    }
}
