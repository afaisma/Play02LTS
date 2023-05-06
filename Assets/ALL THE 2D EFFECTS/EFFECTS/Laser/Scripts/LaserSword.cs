using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{

    // demo script for the laser shooting
    public class LaserSword : MonoBehaviour
    {
        Camera cam;
        [SerializeField] Laser laserScript;

        [SerializeField] float bladeLength = 6f;
        [SerializeField] GameObject laser;
        [SerializeField] Transform shootPoint;

        Quaternion rotation;

        bool powering = false;

        bool poweredOn;


        [SerializeField] float igniteOnSpeed = 10f;
        [SerializeField] float igniteOffSpeed = 20f;

        float igniteProgress = 0;

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
                poweredOn = !poweredOn;
                PowerBeam(poweredOn);
            }

            if (poweredOn || powering)
            {
                UpdateBeam();
            }

            RotateToMouse();
        }

        void PowerBeam(bool enabled)
        {
            powering = true;
            igniteProgress = 0;
            if (enabled) laserScript.PowerBeam(enabled);
        }

        void UpdateBeam()
        {
            var dir = (Vector2)shootPoint.position - (Vector2)transform.position;
            var targetEndPoint = (Vector2)shootPoint.position + (bladeLength * dir);
            var endPoint = targetEndPoint;
            if (powering)
            {
                igniteProgress += Time.deltaTime * igniteOnSpeed;
                if (poweredOn)
                {

                    if (igniteProgress >= 1)
                    {
                        powering = false;
                        igniteProgress = 1;
                    }
                    endPoint = Vector2.Lerp(shootPoint.position, targetEndPoint, igniteProgress);
                    laserScript.SetBeamPoints(shootPoint.position, endPoint);
                }
                else
                {
                    if (igniteProgress >= 1)
                    {
                        igniteProgress = 0;
                        powering = false;
                        laserScript.PowerBeam(false);
                        return;
                    }
                    endPoint = Vector2.Lerp(shootPoint.position, targetEndPoint, 1 - igniteProgress);
                    laserScript.SetBeamPoints(shootPoint.position, endPoint);
                }
            }
            else
            {
                laserScript.SetBeamPoints(shootPoint.position, endPoint);
            }
        }


        void RotateToMouse()
        {
            var direction = cam.ScreenToWorldPoint(Input.mousePosition) - transform.position;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            rotation.eulerAngles = new Vector3(0, 0, angle);
            transform.rotation = rotation;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            UpdateBeam();
        }
#endif
    }
}
