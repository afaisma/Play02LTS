using UnityEngine;

namespace Grogan.Effects2D
{
    public class Snow : MonoBehaviour
    {
        float defaultEmissionSnow = 35f;
        float defaultEmissionFlakes = 25f;

        [Header("Options")]
        [Tooltip("Wich layers do the snow flakes collide with")]
        [SerializeField] LayerMask collisionLayers;

        [Range(0,10)]
        [Tooltip("Determines how long the individual snow particles falls before being remoed. Adjust this so the snow passes below the ground/screen.")]
        [SerializeField] float fallDuration  = 6f;    
        [Header("Snow Particles")]    
        [SerializeField] bool enableNormalSnow = true;
        [SerializeField] bool enableStylisedSnow = false;
        
        [Range(0,20)]
        [SerializeField] float normalSnowDensity = 1f;
        
        [Range(0,20)]
        [SerializeField] float stylisedSnowDensity = 1f;

        [Header("Particle References")]
        [SerializeField] ParticleSystem normalSnow;
        [SerializeField] ParticleSystem stylisedFlakes;

        void Awake()
        {
            if (normalSnow != null)
            {
                if (enableNormalSnow)
                {
                    normalSnow.Play();
                }
                else normalSnow.Stop();
            }

            if (stylisedFlakes != null)
            {
                if (enableStylisedSnow)
                {
                    stylisedFlakes.Play();
                }
                else stylisedFlakes.Stop();
            }
        }

        void Update()
        {

            // turn particles on/off depending on changes to the options
            if (!enableNormalSnow && normalSnow.isPlaying) normalSnow.Stop();
            if (enableNormalSnow && !normalSnow.isPlaying) normalSnow.Play();

            if (!enableStylisedSnow && stylisedFlakes.isPlaying) stylisedFlakes.Stop();            
            if (enableStylisedSnow && !stylisedFlakes.isPlaying) stylisedFlakes.Play();

            if(enableNormalSnow)
            {
                var em = normalSnow.emission;
                em.rateOverTimeMultiplier = normalSnowDensity * defaultEmissionSnow;   
                var n = normalSnow.main;
                n.startLifetimeMultiplier = fallDuration;
                
                var c = normalSnow.collision;
                if(collisionLayers != 0)
                {
                    c.enabled = true;
                    c.collidesWith = collisionLayers;
                }
                else
                {
                    c.enabled = false;
                }
            }

            if(enableStylisedSnow)
            {
                var em = stylisedFlakes.emission;
                em.rateOverTimeMultiplier = stylisedSnowDensity * defaultEmissionFlakes;  
                var n = stylisedFlakes.main;
                n.startLifetimeMultiplier = fallDuration;     
                var c = stylisedFlakes.collision;
                if(collisionLayers != 0)
                {
                    c.enabled = true;
                    c.collidesWith = collisionLayers;
                }
                else
                {
                    c.enabled = false;
                }       
            }

        }

    }
}