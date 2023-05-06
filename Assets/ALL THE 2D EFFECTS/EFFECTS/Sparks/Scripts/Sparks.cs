using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{
    public class Sparks : MonoBehaviour
    {
        

        [Header("Bursts")]
        [Tooltip("How many bursts of sparks to use.")]
        [SerializeField] int numberBursts = 5;

        [SerializeField] short minSparks = 10;
        [SerializeField] short maxSparks = 100;

        [Tooltip("The amount of time between each burst of sparks.")]
        [SerializeField] float timeBetweenBursts = 3f;

        [Range(0,1)]
        [Tooltip("Variance/randomness in the time between bursts. Zero means each burst will be exactly the same amount of time apart. Higher numbers makes the time more random.")]
        [SerializeField] float timeBetweenBurstsVariance;

        [Header("Options")]


        [Range(0, 25)]
        [SerializeField] float distanceMultiplier = 10f;

        [Tooltip("Whether or not to repeat the sequence of bursts on a loop.")]
        [SerializeField] bool repeatOnLoop;

        [Tooltip("Which physics layer(s) the sparks will collide with.")]
        [SerializeField] LayerMask collisionLayers;

        [System.Serializable]
        public struct SparkBurst
        {
            public short NumSparks;
            public float Time;
        }

        ParticleSystem partSys;

        void Awake()
        {
            partSys = GetComponent<ParticleSystem>();
            var coll = partSys.collision;
            var m = partSys.main;
            partSys.Stop();
            m.duration = timeBetweenBursts * numberBursts +1;
            m.loop = repeatOnLoop;
            m.startSpeedMultiplier = distanceMultiplier;

            coll.collidesWith = collisionLayers;
            CreateBursts();
            partSys.Play();
        }

        void CreateBursts()
        {
            var emission = partSys.emission;
            ParticleSystem.Burst[] bursts;
            var burstsList = new List<ParticleSystem.Burst>();
            for (int i = 0; i < numberBursts; i++)
            {
                burstsList.Add(new ParticleSystem.Burst()
                {
                    count = Random.Range(minSparks, maxSparks),
                    time = (timeBetweenBursts * i) + Random.Range(-timeBetweenBurstsVariance,timeBetweenBurstsVariance)
                });
            }
            bursts = burstsList.ToArray();
            emission.SetBursts(bursts);
        }
    }
}