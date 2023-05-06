using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{

    public class Leaves : MonoBehaviour
    {

        [Tooltip("How big the individual leaves will be")]
        [Range(0, 5)]
        [SerializeField] float leafSizeScale = 1f;
        [Tooltip("How fast the leaves move across the screen")]
        [Range(0, 20)]
        [SerializeField] float speedScale = 1f;
        [Tooltip("How many leaves are generated")]
        [Range(0, 20)]
        [SerializeField] float leafDensity = 1f;
        
        
        [SerializeField] bool flipDireciton;

        [SerializeField] ParticleSystem leavesParticle;

        void Awake()
        {
            if(flipDireciton)
                leavesParticle.transform.eulerAngles = new Vector3(-180, leavesParticle.transform.eulerAngles.y, leavesParticle.transform.eulerAngles.z); 
            else
                leavesParticle.transform.eulerAngles = new Vector3(0, leavesParticle.transform.eulerAngles.y, leavesParticle.transform.eulerAngles.z); 
        }

        void Update()
        {
            if(leavesParticle != null && leavesParticle.isPlaying)
            {
                var em = leavesParticle.emission;
                em.rateOverTimeMultiplier = leafDensity;
                var m = leavesParticle.main;
                m.startSpeedMultiplier = speedScale;
                m.startSizeMultiplier = leafSizeScale;
            }
        }

    }

}