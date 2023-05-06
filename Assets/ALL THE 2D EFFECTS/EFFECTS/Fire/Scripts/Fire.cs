using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{
    public class Fire : MonoBehaviour
    {

        [Header("Options")]
        [SerializeField] Vector2 scale = new Vector2(1, 1);

        [Range(0, 100)]
        [SerializeField] float glowLevel = 1f;
        [SerializeField] float gravityScale = -0.25f;
        [SerializeField] float flameLifeScale = 1f;
        [SerializeField] float speedScale = 1f;
        [SerializeField] bool enableEmbers = true;
        [SerializeField] bool enableFlames = true;
        [SerializeField] bool enableAdditive = true;
        [SerializeField] bool preWarm = false;    

        

        [SerializeField] bool isLit = true;

        [Header("Particle References")]

        [SerializeField] ParticleSystem mainFlameParticle;
        [SerializeField] ParticleSystem additiveParticle;
        [SerializeField] ParticleSystem glowParticle;
        [SerializeField] ParticleSystem embersParticle;
        [SerializeField] Transform particlesParent;


        List<ParticleSystem> allParticles;

        public bool IsLit { get { return isLit; } }
        void Awake()
        {
            SetupParticles();
            if (isLit) Ignite();
        }

        void SetupParticles()
        {
            allParticles = new List<ParticleSystem>();
            allParticles.Add(mainFlameParticle);
            allParticles.Add(additiveParticle);
            allParticles.Add(glowParticle);
            allParticles.Add(embersParticle);
        }

        // Update is called once per frame
        void Update()
        {
            ApplySettings();
        }

        public void Ignite()
        {
            if (allParticles == null) SetupParticles();
            foreach (var ps in allParticles)
            {
                if (ps != null)
                {
                    ps.Play();
                }
            }

            if (!enableEmbers)
            {
                embersParticle.Stop();
            }

            if (!enableFlames)
            {
                mainFlameParticle.Stop();
            }

            if (!enableAdditive)
            {
                additiveParticle.Stop();
            }
        }

        public void Extinguish()
        {
            if (allParticles == null) SetupParticles();
            foreach (var ps in allParticles)
            {
                if (ps != null)
                {
                    ps.Stop();
                }
            }
        }


        void ApplySettings()
        {
            if (allParticles == null) SetupParticles();
            foreach (var ps in allParticles)
            {
                if (ps != null)
                {
                    ps.transform.localScale = new Vector3(scale.x, scale.y, 1);
                    var m = ps.main;
                    m.gravityModifierMultiplier = gravityScale;
                    if (gravityScale == 0) m.gravityModifier = 0; // edge case for 0 gravity, e.g. a static fireball like the Sun
                    m.startLifetimeMultiplier = flameLifeScale;
                    m.simulationSpeed = speedScale;
                }

            }
            if (glowParticle != null)
            {
                var g = glowParticle.main;
                g.startSizeMultiplier = glowLevel;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplySettings();
        }

#endif
    }
}