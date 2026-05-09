using UnityEngine;

public class Coin : MonoBehaviour
{
    public int value = 1;

    public float rotationSpeed = 120f;
    public float floatSpeed = 2f;
    public float floatHeight = 0.25f;

    public AudioClip collectSound;

    private Vector3 startPosition;
    private bool collected = false;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        if (collected) return;

        // Spin coin
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);

        // Floating effect
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !collected)
        {
            collected = true;

            CoinCollector.Instance.AddCoin(value);

            // Play sound
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position);
            }

            // Disable collider
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            // Destroy the mesh instantly
            MeshRenderer mesh = GetComponent<MeshRenderer>();
            if (mesh != null)
                Destroy(mesh);

            // Destroy coin object shortly after
            Destroy(gameObject, 0.1f);
        }
    }
}