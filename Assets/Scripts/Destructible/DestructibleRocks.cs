using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class DestructibleRocks : MonoBehaviour
{
    [Header("Rock Pile Shape")]
    [SerializeField] private Vector3Int blocks = new Vector3Int(4, 3, 2);
    [SerializeField] private float blockSize = 0.8f;
    [SerializeField] private Color rockColor = new Color(0.45f, 0.45f, 0.45f);

    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 8f;   
    [SerializeField] private float debrisLifetime = 5f;   
    [SerializeField] private float shrinkDuration = 1f;

    public event System.Action OnDestroyed;               // Mission or objective can listen to this event to know when the rock pile is destroyed

    private BoxCollider blockingCollider;
    private NavMeshObstacle navObstacle;
    private readonly System.Collections.Generic.List<Transform> pieces = new();
    private bool isDestroyed;

    private void Awake()
    {
        BuildPile();
        SetupBlocking();
    }

    private void BuildPile()
    {
        Vector3 size = new Vector3(blocks.x, blocks.y, blocks.z) * blockSize;
        Vector3 corner = new Vector3(-size.x / 2f, 0f, -size.z / 2f);

        for (int x = 0; x < blocks.x; x++)
            for (int y = 0; y < blocks.y; y++)
                for (int z = 0; z < blocks.z; z++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Rock";
                    cube.transform.SetParent(transform, false);

                    Vector3 jitter = Random.insideUnitSphere * blockSize * 0.08f;
                    cube.transform.localPosition = corner + new Vector3(
                        (x + 0.5f) * blockSize, (y + 0.5f) * blockSize, (z + 0.5f) * blockSize) + jitter;
                    cube.transform.localRotation = Quaternion.Euler(
                        Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                    cube.transform.localScale = Vector3.one * blockSize * Random.Range(0.85f, 1f);

                    // block collider is only for the root object, the pieces don't need it before explosion, otherwise they will push each other away 
                    Destroy(cube.GetComponent<Collider>());

                    float shade = Random.Range(0.8f, 1.1f);
                    cube.GetComponent<Renderer>().material.color = rockColor * shade;

                    pieces.Add(cube.transform);
                }
    }

    // A big collider + NavMeshObstacle（Carve）to block the road
    private void SetupBlocking()
    {
        Vector3 size = new Vector3(blocks.x, blocks.y, blocks.z) * blockSize;
        Vector3 center = new Vector3(0f, size.y / 2f, 0f);

        blockingCollider = gameObject.AddComponent<BoxCollider>();
        blockingCollider.center = center;
        blockingCollider.size = size;

        navObstacle = gameObject.AddComponent<NavMeshObstacle>();
        navObstacle.shape = NavMeshObstacleShape.Box;
        navObstacle.center = center;
        navObstacle.size = size;
        navObstacle.carving = true;
    }

    public void Explode(Vector3 explosionPos, float radius)
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // 1. Open the way, cancel the blocking collider and navmesh obstacle
        blockingCollider.enabled = false;
        navObstacle.enabled = false;

        // 2. Debris physics: detach each piece, add collider and rigidbody, apply explosion force, and start shrinking coroutine
        foreach (Transform piece in pieces)
        {
            piece.SetParent(null, true);

            BoxCollider col = piece.gameObject.AddComponent<BoxCollider>();
            Rigidbody rb = piece.gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.AddExplosionForce(explosionForce, explosionPos, radius, 0.5f, ForceMode.Impulse);

            // Start shrinking coroutine after a random delay, so the debris will shrink and disappear after a while
            piece.gameObject.AddComponent<DebrisCleanup>().Begin(debrisLifetime, shrinkDuration);
        }

        OnDestroyed?.Invoke();
        Destroy(gameObject); // Note: The root object is destroyed immediately, but the debris pieces will continue to exist and shrink over time
    }
}

// Debris Cleanup: Shrink and destroy pieces after a delay
public class DebrisCleanup : MonoBehaviour
{
    public void Begin(float delay, float duration)
    {
        StartCoroutine(Run(delay + Random.Range(0f, 1.5f), duration));
    }

    private IEnumerator Run(float delay, float duration)
    {
        yield return new WaitForSeconds(delay);

        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, Vector3.zero, t / duration);
            yield return null;
        }
        Destroy(gameObject);
    }
}