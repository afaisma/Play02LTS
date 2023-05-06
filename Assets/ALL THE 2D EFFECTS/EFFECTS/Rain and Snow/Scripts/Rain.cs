using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{
    public class Rain : MonoBehaviour
    {
        [Header("Options")]
        [Range(0,50)]
        [Tooltip("This increases or decreases how fast the raindrops fall. This affects both layers.")]
        [SerializeField] float fallSpeedMultiplier = 2f;
        [Range(0,50)]
        [Tooltip("This changes the rate that raindrops are generated.")]
        [SerializeField] float amountOfRainMultiplier = 1f;    
        
        [Header("First Layer")]
        [SerializeField] bool enableFirstLayer;
        [SerializeField] LayerMask splashCollisionLayersFirstLayer;
        [Tooltip("Reduce this to balance performance on low-powered hardware. This sets a maximum number of raindrops on screen at once.")]
        [SerializeField] int particleMaxLimitFirstLayer = 10000;
        [Tooltip("This determines how long each raindrop is active before disappearing. A lower number can increase performance, but make sure the value is high enough for the raindrops to fall down far enough so they don't disappear mid-air.")]
        [SerializeField] float maxParticleDurationFirstLayer = 2f;
        
        [Header(header: "Second Layer")]
        [SerializeField] bool enableSecondLayer;
        [SerializeField] LayerMask splashCollisionLayersSecondLayer;    
        [Tooltip("Reduce this to balance performance on low-powered hardware. This sets a maximum number of raindrops on screen at once.")]
        [SerializeField] int particleMaxLimitSecondLayer = 10000;
        [Tooltip("This determines how long each raindrop is active before disappearing. A lower number can increase performance, but make sure the value is high enough for the raindrops to fall down far enough so they don't disappear mid-air.")]
        [SerializeField] float maxParticleDurationSecondLayer = 2f;   

        [Header("References")]
        [SerializeField] ParticleSystem firstLayer;
        [SerializeField] ParticleSystem secondLayer;


        float defaultEmissionRate = 200f;

        void Update()
        {

            // turn particles on/off depending on changes to the options
            if (!enableFirstLayer && firstLayer.isPlaying) firstLayer.Stop();
            if (enableFirstLayer && !firstLayer.isPlaying) firstLayer.Play();

            if (!enableSecondLayer && secondLayer.isPlaying) secondLayer.Stop();
            if (enableSecondLayer && !secondLayer.isPlaying) secondLayer.Play();


            if(enableFirstLayer)
            {
                var em = firstLayer.emission;
                em.rateOverTimeMultiplier = amountOfRainMultiplier * defaultEmissionRate;    
                var c = firstLayer.collision;
                c.collidesWith = splashCollisionLayersFirstLayer;   
                var m = firstLayer.main;
                m.gravityModifier = fallSpeedMultiplier;   
                m.maxParticles = particleMaxLimitFirstLayer;    
                m.startLifetime = maxParticleDurationFirstLayer;   
            }

            if(enableSecondLayer)
            {
                var em = secondLayer.emission;
                em.rateOverTimeMultiplier = amountOfRainMultiplier * defaultEmissionRate;
                var c = secondLayer.collision;
                c.collidesWith = splashCollisionLayersSecondLayer;    
                var m = secondLayer.main;
                m.gravityModifier = fallSpeedMultiplier;    
                m.maxParticles = particleMaxLimitSecondLayer;  
                m.startLifetime = maxParticleDurationSecondLayer;        
            }

            // if(enableHeavyRain)
            // {
            //     var em = heavyRain.emission;
            //     em.rateOverTimeMultiplier = heavyRainIntensity * defaultEmissionRate;
            // }
        }
    }
}