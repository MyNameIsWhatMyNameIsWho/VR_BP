using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomTrailRenderer : MonoBehaviour
{
	[SerializeField] private int trailResolution; //number of points on line renderer
	[SerializeField] private float offset;	//distance between points of line renderer
	[SerializeField] private float lagTime; //time before moving to the next position

	private LineRenderer lineRenderer;

	List<Vector3> positions;
	List<Vector3> velocities;

	void Start()
	{
		positions = new List<Vector3>();
		velocities = new List<Vector3>();

		lineRenderer = GetComponent<LineRenderer>();
		lineRenderer.positionCount = trailResolution;

		positions.Add(transform.position);
		velocities.Add(Vector3.zero);

		for (int i = 1; i < trailResolution; i++)
		{
			Vector3 position;
			Vector3 velocity = Vector3.zero;

			position = transform.position + (transform.right * (offset * i));

			positions.Add(position);
			velocities.Add(velocity); 
		}
	}

   void Update() {
		positions[0] = transform.position;
		lineRenderer.SetPosition(0, positions[0]);

		for (int i = 1; i < trailResolution; i++)
		{
			Vector3 segmentVelocity = velocities[i];
			positions[i] = Vector3.SmoothDamp(positions[i], positions[i - 1] + (transform.right * offset), ref segmentVelocity, lagTime);
			velocities[i] = segmentVelocity;

			lineRenderer.SetPosition(i, positions[i]);
		}
	}
}

