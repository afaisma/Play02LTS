using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurveBehaviour : MonoBehaviour
{
    public List<Vector3> waypoints = new List<Vector3>();
    public float speed = 2.5f;

    private int _currentWaypointIndex = 0;
    private Coroutine _moveCoroutine;
    private bool _isPaused = true;

    public void Pause()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        _isPaused = true;
        //Debug.Log("Paused");
    }

    public void Resume(bool forward = true)
    {
        if (!_isPaused) return;

        _isPaused = false;
        _moveCoroutine = StartCoroutine(Move(forward));
    }

    public void Reset()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        _currentWaypointIndex = 0;
        transform.position = waypoints[0];
    }

    public void DirectMovement(Vector3 from, Vector3 to, int nWaypoints)
    {
        ClearWaypoints();
        Vector3 pos = transform.position;
        waypoints.Add(new Vector3(pos.x, pos.y, pos.z));
        for (int i = 0; i < nWaypoints; i++)
        {
            float t = i / (float)(nWaypoints - 1);
            Vector3 waypoint = Vector3.Lerp(from, to, t);
            waypoints.Add(waypoint);
        }
        waypoints.Add(new Vector3(to.x, to.y, to.z));
    }

    public void WavyMovement(Vector3 from, Vector3 to, int nWaypoints, int direction)
    {
        ClearWaypoints();
        Vector3 pos = transform.position;
        waypoints.Add(new Vector3(pos.x, pos.y, pos.z));
        for (int i = 0; i < nWaypoints; i++)
        {
            float t = i / (float)(nWaypoints - 1);
            Vector3 waypoint = Vector3.Lerp(from, to, t);
            if (direction == 0)
            {
                waypoint.y += Mathf.Sin(t * Mathf.PI * 4) * 0.5f;
                waypoint.x += Mathf.Cos(t * Mathf.PI * 4) * 0.5f;
            }
            else
            {
                waypoint.y += Mathf.Cos(t * Mathf.PI * 4) * 0.5f;
                waypoint.x += Mathf.Sin(t * Mathf.PI * 4) * 0.5f;
            }

            waypoints.Add(waypoint);
        }
        waypoints.Add(new Vector3(to.x, to.y, to.z));
    }

    private void ClearWaypoints()
    {
        waypoints.Clear();
    }

    public void MoveForward()
    {
        if (waypoints.Count < 2)
        {
            Debug.LogError("Insufficient waypoints for movement!");
            return;
        }

        _currentWaypointIndex = 0;
        Resume(true);
    }

    public void MoveBackward()
    {
        if (waypoints.Count < 2)
        {
            Debug.LogError("Insufficient waypoints for movement!");
            return;
        }

        _currentWaypointIndex = waypoints.Count - 1;
        Resume(false);
    }

    private IEnumerator Move(bool forward)
    {
        while (forward ? _currentWaypointIndex < waypoints.Count - 1 : _currentWaypointIndex > 0)
        {
            int nextWaypointIndex = forward ? _currentWaypointIndex + 1 : _currentWaypointIndex - 1;
            float distanceToNextWaypoint = Vector3.Distance(waypoints[_currentWaypointIndex], waypoints[nextWaypointIndex]);
            float timeToNextWaypoint = distanceToNextWaypoint / speed + 0.01f;

            for (float t = 0f; t <= timeToNextWaypoint; t += Time.deltaTime)
            {
                if (_isPaused)
                {
                    yield break;
                }

                float progress = t / timeToNextWaypoint;
                Vector3 newPosition = Vector3.Lerp(waypoints[_currentWaypointIndex], waypoints[nextWaypointIndex], progress);
                newPosition.z = transform.position.z;
                transform.position = newPosition;
                yield return null;
            }

            _currentWaypointIndex = nextWaypointIndex;
        }
        
        transform.position = forward ? waypoints[^1] : waypoints[0];
        Pause();
    }
    
    public void MoveTo(float proportionOfTheWay)
    {
        if (proportionOfTheWay < 0 || proportionOfTheWay > 1)
        {
            Debug.LogError("Invalid value for proportionOfTheWay. It should be between 0 and 1.");
            return;
        }

        // Calculate the index of the waypoint the object should be at
        int waypointIndex = Mathf.FloorToInt(proportionOfTheWay * (waypoints.Count - 1));

        // Calculate the proportion of the distance to the next waypoint the object should be at
        float waypointProportion = (proportionOfTheWay * (waypoints.Count - 1)) % 1;

        // If we're not at the last waypoint, interpolate between the current and next waypoints
        if (waypointIndex < waypoints.Count - 1)
        {
            transform.position = Vector3.Lerp(waypoints[waypointIndex], waypoints[waypointIndex + 1], waypointProportion);
        }
        // If we're at the last waypoint, just set the position to the last waypoint's position
        else
        {
            transform.position = waypoints[waypointIndex];
        }
    }

    public static void TestCurveBehaviour()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        CurveBehaviour curveBehaviour = go.AddComponent<CurveBehaviour>();
        curveBehaviour.speed = 15f;
        Vector3 waypoint1 = new Vector3(0, 0, 0);
        Vector3 waypoint3 = new Vector3(0, -10, 0);
        curveBehaviour.WavyMovement(waypoint1, waypoint3, 10, 1);
        curveBehaviour.MoveForward();
        curveBehaviour.StartCoroutine(WaitUntilPausedAndMoveBackward(curveBehaviour));
    }
    private static IEnumerator WaitUntilPausedAndMoveBackward(CurveBehaviour curveBehaviour)
    {
        yield return new WaitUntil(() => curveBehaviour._isPaused);
        curveBehaviour.MoveBackward();
    }
}
