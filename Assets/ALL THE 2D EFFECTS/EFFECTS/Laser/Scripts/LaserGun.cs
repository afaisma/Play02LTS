using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{

    // demo script for the laser shooting
    public class LaserGun : MonoBehaviour
    {
        Camera cam;
        [SerializeField] Laser laserScript;
        [SerializeField] GameObject laser;
        [SerializeField] Transform shootPoint;
        [SerializeField] LayerMask hitMask;

        Quaternion rotation;

        void Awake()
        {
            cam = Camera.main;
            laser.SetActive(true);
            PowerBeam(false);
        }

        void Update()
        {
            if (Input.GetButtonDown("Fire1"))
            {
                PowerBeam(true);
            }

            if (Input.GetButton("Fire1"))
            {
                UpdateBeam();
            }

            if (Input.GetButtonUp("Fire1"))
            {
                PowerBeam(false);
            }

            RotateToMouse();
        }

        void PowerBeam(bool enabled)
        {
            laserScript.PowerBeam(enabled);
        }

        void UpdateBeam()
        {

            var mousePos = (Vector2)cam.ScreenToWorldPoint(Input.mousePosition);
            var endPoint = mousePos;

            var dir = mousePos - (Vector2)transform.position;
            var hit = Physics2D.Raycast((Vector2)transform.position, dir.normalized, dir.magnitude, hitMask);
            if (hit)
            {
                endPoint = hit.point;
            }

            laserScript.SetBeamPoints(shootPoint.position, endPoint);
        }



        void RotateToMouse()
        {
            var direction = cam.ScreenToWorldPoint(Input.mousePosition) - transform.position;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            rotation.eulerAngles = new Vector3(0, 0, angle);
            transform.rotation = rotation;
        }
    }
}