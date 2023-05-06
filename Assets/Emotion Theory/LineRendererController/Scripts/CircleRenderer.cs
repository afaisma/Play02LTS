using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
[RequireComponent (typeof(LineRenderer))]
public class CircleRenderer : MonoBehaviour
{
	public bool DynamicSegmentCount;

	[Range (0, 360)]
	public int segments = 50;

	public int segs
	{ get { return DynamicSegmentCount ? (int)(segments * radius) : segments; } }

	public float radius = 5;

	[Range (0, 5)]
	public float xScale = 5;

	[Range (0, 5)]
	public float yScale = 5;

	public LineRenderer Line;

	public bool UpdateInRealtime;

	void Start ()
	{
		DoRenderer ();
	}

	void Update ()
	{
		if (UpdateInRealtime)
			DoRenderer ();
	}

	public void SetSize (float radius, bool applyRender = true)
	{
		this.radius = radius;

		if (applyRender)
			DoRenderer ();
	}

	public void DoRenderer ()
	{
		if (!Line)
			Line = gameObject.GetComponent<LineRenderer> ();

		if (!Line)
			return;

		Line.positionCount = segs + 1;
		CreatePoints ();
	}

	void CreatePoints ()
	{
		float x;
		float y;
		float z;

		float angle = 20f;

		for (int i = 0; i < segs + 1; i++)
		{
			x = Mathf.Sin (Mathf.Deg2Rad * angle) * (radius * xScale);
			y = Mathf.Cos (Mathf.Deg2Rad * angle) * (radius * yScale);
			z = 0;

			Line.SetPosition (i, new Vector3 (x, y, z));

			angle += (360f / segs);
		}
	}
}