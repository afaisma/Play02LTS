using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[ExecuteInEditMode]
public class LineRendererController : MonoBehaviour
{
//	[HideInInspector]
	private LineRenderer Line;
	[Header("References")]
	public Transform TransformBase;
	public Transform TransformTip;

//	[HideInInspector]
	public List<Vector3> Points;

	[Header("Parameters")]
	[Range (0f, 1f)]
	public float StartFill = 0;
	[Range (0f, 1f)]
	public float Fill = 1;
	public bool FillIsRelative = false;
	public bool UpdateRealtime = false;

	public Vector3 PointBase
	{
		get
		{
			Vector3 p = this.Points[0];

			if (Line.positionCount > 0)
				p = Line.GetPosition (0);

			if (Line.useWorldSpace)
				return p;
			else
				return Line.transform.TransformPoint (p);
		}
	}

	public Vector3 PointTip
	{
		get
		{
			var p = Line.GetPosition (Line.positionCount - 1);

			if (Line.useWorldSpace)
				return p;
			else
				return Line.transform.TransformPoint (p);
		}
	}

	void Awake ()
	{
		if (!Line)
			Line = GetComponent <LineRenderer> ();

		if (!Line)
			return;

		if (Points == null || Points.Count == 0)
			Points = GetAllPositions (Line);
	}

	// Use this for initialization
	void Start ()
	{
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		if (!Line)
			return;

		if (UpdateRealtime)
			UpdateLineRenderer ();
	}

	void UpdateLineRenderer ()
	{
		var pts = GetPositionsFromFill (this.Points, this.Fill, this.StartFill, this.FillIsRelative).ToArray ();
		Line.positionCount = pts.Length;
		Line.SetPositions ( pts );

		if (TransformBase)
			TransformBase.position = this.PointBase;

		if (TransformTip)
			TransformTip.position = this.PointTip;
	}

	public void SetPositions (List<Vector3> points, bool updateRenderer = true)
	{
		this.Points = points;

		if (updateRenderer)
			UpdateLineRenderer ();
	}

	public void SetStartFill(float amt)
	{
		StartFill = amt;

		UpdateLineRenderer ();
	}

	public void SetFill(float amt)
	{
		Fill = amt;

		UpdateLineRenderer ();
	}

	// STATIC METHODS

	public static List<Vector3> GetPositionsFromFill (List<Vector3> pts, float amt, float start = 0, bool relative = true)
	{
		if (pts == null || pts.Count < 2)
			return new List<Vector3>();

		// Clamp the normalized fill amount.
		if (relative)
		{
			// amt needs to be the final. So, if we're relative, the "final" is start + amt
			amt = start + amt;
		}
		else
		{

		}

		start = Mathf.Clamp01 (start);
		amt = Mathf.Clamp01 (amt);


		float totalDist = GetLineLength (pts);
		float startDist = start * totalDist;
		float curDist = amt * totalDist;

		List<Vector3> finalPoints = new List<Vector3> ();

		float d = 0;
		int i = 1;
		float lf = 0;

		// First point.
		if (start == 0)
			finalPoints.Add (pts [0]);
		else
		{
			// Get first point.
			for (i = 1; i < pts.Count; i++)
			{
				float ld = Vector3.Distance (pts[i-1], pts[i]);

				// Have we passed the point we need to reach?
				if (!(startDist > (d + ld)))
				{
					// YES
					lf = (startDist - d) / ld;
					finalPoints.Add ( pts[i-1] + (pts[i] - pts[i-1]) * lf );
					break;
				}
				else
				{
					// NO
					//					finalPoints.Add (pts[i]);
					d += ld;
					continue;
				}
			}
		}

		// Get the end point.
		for (int j = i; j < pts.Count; j++)
		{
			float ld = Vector3.Distance (pts[j-1], pts[j]);

			// Have we passed the point we need to reach?
			if (!(curDist > (d + ld)))
			{
				// YES
				lf = (curDist - d) / ld;
				finalPoints.Add ( pts[j-1] + (pts[j] - pts[j-1]) * lf );
				break;
			}
			else
			{
				// NO
				finalPoints.Add (pts[j]);
				d += ld;
				continue;
			}
		}

		//		float d = 0;
		//		int i = 1;
		//		float lf = 0;
		//		for (i = 1; i < pts.Count; i++)
		//		{
		//			float ld = Vector3.Distance (pts[i-1], pts[i]);
		//
		//			// Have we passed the point we need to reach?
		//			if (!(curDist > (d + ld)))
		//			{
		//				// YES
		//				lf = (curDist - d) / ld;
		//				finalPoints.Add ( pts[i-1] + (pts[i] - pts[i-1]) * lf );
		//				break;
		//			}
		//			else
		//			{
		//				// NO
		//				finalPoints.Add (pts[i]);
		//				d += ld;
		//				continue;
		//			}
		//		}

		//		Debug.Log ( string.Format ("Count [{0}] of [{1}] with lf as [{2}]", i, pts.Count, lf));

		return finalPoints;
	}

	public static List<Vector3> GetAllPositions (LineRenderer line)
	{
		var list = new List<Vector3> (line.positionCount);

		for (int i = 0; i < line.positionCount; i++)
			list.Add (line.GetPosition (i)); 

		return list;
	}

	public static float GetLineLength ( List<Vector3> points )
	{
		float d = 0;

		for (int i = 1; i < points.Count; i++)
			d += Vector3.Distance (points [i - 1], points [i]);

		return d;
	}
}
