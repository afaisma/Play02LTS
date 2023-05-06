using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LineRendererSample : MonoBehaviour {

	public LineRendererController lrc;

	public float speed = 1f;
	public float multiplier = 1f;
	public float xOffset;
	public float yOffset;

	void Start ()
	{
		if (!lrc)
			lrc = GetComponent <LineRendererController> ();
	}

	// Update is called once per frame
	void Update () 
	{
		lrc.StartFill = yOffset + Mathf.Sin (Time.time * speed + xOffset) * multiplier;
	}
}
