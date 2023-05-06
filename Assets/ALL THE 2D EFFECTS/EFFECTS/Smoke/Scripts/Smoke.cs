using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{

    public class Smoke : MonoBehaviour
    {
        [SerializeField] float density = 40f;

        [SerializeField] ParticleSystem smokeParticle;

        void Update()
        {
            if (smokeParticle != null)
            {
                var se = smokeParticle.emission;
                se.rateOverTimeMultiplier = density;
            }
        }
    }
}