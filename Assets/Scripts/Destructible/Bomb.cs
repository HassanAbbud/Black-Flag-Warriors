using System.Collections;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    [SerializeField] private float fuseTime = 2f;
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float pushForce = 8f;

    // create a bomb at the specified position
    public static Bomb Spawn(Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bomb";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.4f;
        go.GetComponent<Renderer>().material.color = Color.black;
        go.AddComponent<Rigidbody>();
        return go.AddComponent<Bomb>();
    }

    private void Start()
    {
        StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        // flash the bomb's color between red and black to indicate it's about to explode
        Renderer r = GetComponent<Renderer>();
        float timer = 0f;
        while (timer < fuseTime)
        {
            timer += Time.deltaTime;
            r.material.color = Mathf.Repeat(timer * 6f, 1f) > 0.5f ? Color.red : Color.black;
            yield return null;
        }
        yield return Detonate();
    }

    private IEnumerator Detonate()
    {
        Vector3 pos = transform.position;

        // Turn off the bomb's collider and rigidbody to avoid affecting the debris physics
        GetComponent<Collider>().enabled = false;
        Destroy(GetComponent<Rigidbody>());

        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius);
        foreach (Collider hit in hits)
        {
            DestructibleRocks rocks = hit.GetComponentInParent<DestructibleRocks>();
            if (rocks != null)
                rocks.Explode(pos, explosionRadius);

            Rigidbody rb = hit.attachedRigidbody;
            if (rb != null)
                rb.AddExplosionForce(pushForce, pos, explosionRadius, 0.5f, ForceMode.Impulse);
        }

        // explosion visual effect: a quick orange sphere that expands and disappears
        GetComponent<Renderer>().material.color = new Color(1f, 0.6f, 0.1f);
        float t = 0f, duration = 0.25f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(0.4f, explosionRadius * 2f, t / duration);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}