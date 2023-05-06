using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{
    public class LaserGate : MonoBehaviour
    {
        [SerializeField] Transform emitPoint;
        [SerializeField] Transform endPoint;

        [SerializeField] Laser laser;

        [SerializeField] bool isOn;

        [SerializeField] bool useCollider = true;
        [SerializeField] EdgeCollider2D coll;


        void Start()
        {
            coll.enabled = isOn && useCollider;
            laser.PowerBeam(isOn);
        }

        void Update()
        {
            if (isOn != laser.IsOn)
            {
                laser.PowerBeam(isOn);
                coll.enabled = isOn && useCollider;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SetPoints();
        }
#endif

        void SetPoints()
        {
            laser.SetBeamPoints(emitPoint.position, endPoint.position);
            if (useCollider && isOn)
            {
                coll.SetPoints(new List<Vector2>() { emitPoint.position - transform.position, endPoint.position - transform.position });
                coll.enabled = true;
            }
            else
            {
                coll.enabled = false;
            }
        }

        private void OnDrawGizmos()
        {
            SetPoints();
        }
    }
}
