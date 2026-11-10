using System.Collections.Generic;
using UnityEngine;

public class MinimapController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera minimapCamera;
    public Transform gameplayCamera;       // main camera, used for rotation
    public RectTransform iconRoot;         // root node for icons that fills the minimap
    public RectTransform[] layerRoots;     // three layers: Low / Mid / High
    public RectTransform northMark;        // optional
    public MinimapIcon iconPrefab;
    public Sprite[] iconSprites;           // sprites in the order of MarkerType

    [Header("Small Map")]
    public bool rotateWithCamera = true;   // whether the map rotates with the main camera
    public float cameraHeight = 100f;
    public float edgePadding = 12f;        // pixel padding when snapping to edges

    [Header("Big Map")]
    public KeyCode bigMapKey = KeyCode.M;
    public float smallRadius = 60f;        // small map view radius (meters)
    public float bigRadius = 300f;         // big map view radius (meters)
    public float zoomSpeed = 8f;           // zoom smoothness, the higher the faster
    public bool bigMapFixedNorth = false;  // whether to fix north in big map
    public bool hideSoldiersInBigMap = true; // hide soldier icons in big map to avoid overcrowding

    // current view radius (value during smooth transition)
    float viewRadius;
    float targetRadius;
    bool isBig;
    float halfSize;

    static readonly List<MinimapMarker> markers = new List<MinimapMarker>();
    readonly Dictionary<MinimapMarker, MinimapIcon> active = new Dictionary<MinimapMarker, MinimapIcon>();
    readonly Stack<MinimapIcon> pool = new Stack<MinimapIcon>();
    readonly List<MinimapMarker> toRelease = new List<MinimapMarker>();

    // clear the static list when Domain Reload is disabled
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => markers.Clear();

    public static void Register(MinimapMarker m) { if (!markers.Contains(m)) markers.Add(m); }
    public static void Unregister(MinimapMarker m) { markers.Remove(m); }

    void Start()
    {
        minimapCamera.orthographic = true;
        viewRadius = smallRadius;
        targetRadius = smallRadius;
    }

    void Update()
    {
        // If using the new Input System, remove this line and call ToggleBigMap() from an InputAction callback
        if (Input.GetKeyDown(bigMapKey)) ToggleBigMap();

        // Smoothly zoom the view (unaffected by timeScale)
        viewRadius = Mathf.Lerp(viewRadius, targetRadius,
                                1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));
    }

    void LateUpdate()
    {
        halfSize = iconRoot.rect.width * 0.5f;
        UpdateCamera();
        UpdateIcons();
        UpdateNorthMark();
    }

    // ---------- Big map ----------
    public void ToggleBigMap() => ApplyMode(!isBig);

    void ApplyMode(bool big)
    {
        isBig = big;
        targetRadius = big ? bigRadius : smallRadius;
    }

    // Map orientation: rotates with the camera, big map can optionally fix north
    float Yaw => (rotateWithCamera && !(isBig && bigMapFixedNorth))
                 ? gameplayCamera.eulerAngles.y : 0f;

    // ---------- Camera ----------
    void UpdateCamera()
    {
        // The orthographic size is the half side length, which matches the square view exactly
        minimapCamera.orthographicSize = viewRadius;
        minimapCamera.transform.position =
            new Vector3(player.position.x, cameraHeight, player.position.z);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, Yaw, 0f);
    }

    // ---------- North Mark ----------
    void UpdateNorthMark()
    {
        if (northMark == null) return;
        float rad = Yaw * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
        dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));   // project onto the square edge
        northMark.anchoredPosition = dir * (halfSize - 10f);
    }

    // ---------- Icons ----------
    // 0 = Low, 1 = Mid, 2 = High
    static int LayerOf(MinimapMarker m)
    {
        switch (m.type)
        {
            case MarkerType.Player:
                return 2;                          // player on the top layer

            case MarkerType.Base:
                return m.InBattle ? 2 : 1;         // bases in battle are brought to the top

            case MarkerType.Boss:
            case MarkerType.Neutral:
            case MarkerType.Objective:
                return 1;                          // middle layer

            default:                               // Ally, Enemy
                return 0;                          // bottom layer (soldiers)
        }
    }

    void UpdateIcons()
    {
        // Recycle icons whose markers have been unregistered
        toRelease.Clear();
        foreach (var kv in active)
            if (kv.Key == null || !kv.Key.isActiveAndEnabled) toRelease.Add(kv.Key);
        foreach (var m in toRelease) Release(m);

        Quaternion inv = Quaternion.Euler(0, -Yaw, 0);
        float limit = halfSize - edgePadding;

        foreach (var m in markers)
        {
            // Hide soldier icons in the big map
            if (isBig && hideSoldiersInBigMap &&
                (m.type == MarkerType.Enemy || m.type == MarkerType.Ally))
            {
                Release(m);
                continue;
            }

            Vector3 offset = m.transform.position - player.position;
            offset.y = 0;
            Vector3 local = inv * offset;
            Vector2 p = new Vector2(local.x, local.z) / viewRadius;   // normalized to [-1, 1]

            float maxAxis = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
            bool outside = maxAxis > 1f;

            if (outside)
            {
                // Bases in battle stay visible at the edge even when out of view
                bool keep = m.clampToEdge || m.InBattle;
                if (!keep) { Release(m); continue; }
                p /= maxAxis;                                          // clamp to the square edge
            }

            MinimapIcon icon = GetOrCreate(m);

            int layer = LayerOf(m);
            if (icon.transform.parent != layerRoots[layer])
            {
                icon.transform.SetParent(layerRoots[layer], false);
                if (m.type == MarkerType.Boss) icon.transform.SetAsLastSibling();
            }

            var rt = (RectTransform)icon.transform;
            rt.anchoredPosition = outside ? p * limit : p * halfSize;

            if (m.rotateWithTarget)
                rt.localRotation = Quaternion.Euler(0, 0, -(m.transform.eulerAngles.y - Yaw));

            icon.Tick(m);
        }
    }

    MinimapIcon GetOrCreate(MinimapMarker m)
    {
        if (active.TryGetValue(m, out var icon)) return icon;
        icon = pool.Count > 0 ? pool.Pop() : Instantiate(iconPrefab, layerRoots[0]);
        icon.gameObject.SetActive(true);
        icon.Bind(m, iconSprites[(int)m.type]);
        active[m] = icon;
        return icon;
    }

    void Release(MinimapMarker m)
    {
        if (m == null || !active.TryGetValue(m, out var icon)) return;
        icon.gameObject.SetActive(false);
        pool.Push(icon);
        active.Remove(m);
    }
}