using UnityEngine;

public class VectorDrawing : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private float timeCounter = 0.0f;
    private float animationSpeed = 2.0f;

    void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Update()
    {
        timeCounter += Time.deltaTime * animationSpeed;

        float radius = Mathf.PingPong(timeCounter, 5.0f) + 0.5f;
        DrawPolygon(radius, 6, 0.1f, 0.1f, Color.red);

        int numberOfLines = 500;
        int lineMaxLength = 3;
        int lineMinLength = 1;
        float minX = -5;
        float minY = -5;
        float maxX = 5;
        float maxY = 5;
        //DrawRandomLines(numberOfLines, lineMaxLength, lineMinLength, minX, minY, maxX, maxY);
    }

    void DrawPolygon(float radius, int numberOfPoints, float startWidth, float endWidth, Color lineColor)
    {
        lineRenderer.positionCount = numberOfPoints + 1;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        float angle = 2 * Mathf.PI / numberOfPoints;
        for (int i = 0; i < numberOfPoints + 1; i++)
        {
            float x = radius * Mathf.Cos(angle * i);
            float y = radius * Mathf.Sin(angle * i);
            Vector3 pointPosition = new Vector3(x, y, 0);
            lineRenderer.SetPosition(i, pointPosition);
        }
    }

    void DrawRandomLines(int numberOfLines, int lineMaxLength, int lineMinLength, float minX, float minY, float maxX, float maxY)
    {
        for (int i = 0; i < numberOfLines; i++)
        {
            float randomX1 = Random.Range(minX, maxX);
            float randomY1 = Random.Range(minY, maxY);
            float randomX2 = Random.Range(minX, maxX);
            float randomY2 = Random.Range(minY, maxY);
            Vector3 startPoint = new Vector3(randomX1, randomY1, 0);
            Vector3 endPoint = new Vector3(randomX2, randomY2, 0);
            float lineLength = Vector3.Distance(startPoint, endPoint);

            if (lineLength >= lineMinLength && lineLength <= lineMaxLength)
            {
                Color randomColor = new Color(Random.value, Random.value, Random.value);
                lineRenderer.startColor = randomColor;
                lineRenderer.endColor = randomColor;
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, startPoint);
                lineRenderer.SetPosition(1, endPoint);
            }
        }
    }

}
