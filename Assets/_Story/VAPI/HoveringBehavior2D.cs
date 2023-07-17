        using UnityEngine;

public class HoveringBehavior2D : MonoBehaviour
{
    public float hoverSpeed = 1f; // Speed of the hover
    public float hoverRadius = 1f; // Radius of the hover
    public float hoverHeight = 1f; // How high the object hovers
    public float randomFactor = 0.5f; // Degree of randomness

    private Vector3 _centerPoint;
    private float _randomOffset;

    private void Start()
    {
        _centerPoint = transform.position;
        // Initialize a random offset to use with Perlin noise for randomness
        _randomOffset = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        // Apply Perlin noise to create smooth randomness
        float random = (Mathf.PerlinNoise(Time.time * hoverSpeed + _randomOffset, _randomOffset) - 0.5f) * 2 * randomFactor;

        // Calculate the new position
        float x = Mathf.Cos(Time.time * hoverSpeed + random) * hoverRadius;
        float y = Mathf.Sin(Time.time * hoverSpeed + random) * hoverHeight;

        // Apply the new position
        transform.position = _centerPoint + new Vector3(x, y, 0);
    }
}