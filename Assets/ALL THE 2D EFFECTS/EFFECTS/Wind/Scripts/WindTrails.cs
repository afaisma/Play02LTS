using UnityEngine;

namespace Grogan.Effects2D
{

    public class WindTrails : MonoBehaviour
    {
        [SerializeField] bool showLoopingParticles = true;
        [SerializeField] bool showNormalParticles = true;
        [SerializeField] ParticleSystem normalParticles;
        [SerializeField] ParticleSystem loopingParticles;

        [Range(0, 3)]
        [SerializeField] float sizeMultiplier = 0.5f;
        [Range(0, 10)]
        [SerializeField] float windIntensity = 1f;
        [Range(0, 10)]
        [SerializeField] float speedMultiplier = 1f;

        [SerializeField] bool flipDireciton;

        void Awake()
        {
            if(flipDireciton)
            {
                normalParticles.transform.eulerAngles = new Vector3(-180, normalParticles.transform.eulerAngles.y, normalParticles.transform.eulerAngles.z); 
                loopingParticles.transform.eulerAngles = new Vector3(-180, loopingParticles.transform.eulerAngles.y, loopingParticles.transform.eulerAngles.z); 

            }
            else
            {
                normalParticles.transform.eulerAngles = new Vector3( 0, normalParticles.transform.eulerAngles.y, normalParticles.transform.eulerAngles.z); 
                loopingParticles.transform.eulerAngles = new Vector3( 0, loopingParticles.transform.eulerAngles.y, loopingParticles.transform.eulerAngles.z); 
            }
        }

        void Update()
        {
            if (!showLoopingParticles && loopingParticles != null) loopingParticles.Stop();
            if (!showNormalParticles && normalParticles != null) normalParticles.Stop();

            if (showLoopingParticles && loopingParticles != null)
            {
                var em = loopingParticles.emission;
                em.rateOverTimeMultiplier = windIntensity;

                var m = loopingParticles.main;
                m.startSpeedMultiplier = speedMultiplier;
                m.startSizeMultiplier = sizeMultiplier;

                var t = loopingParticles.trails;
                t.lifetimeMultiplier = sizeMultiplier / 2f;
                loopingParticles.Play();
            }

            if (showNormalParticles && normalParticles != null)
            {
                var em = normalParticles.emission;
                em.rateOverTimeMultiplier = windIntensity;
                var m = normalParticles.main;
                m.startSizeMultiplier = sizeMultiplier;
                m.startSpeedMultiplier = speedMultiplier;
                var t = normalParticles.trails;
                t.lifetimeMultiplier = sizeMultiplier / 2f;
                normalParticles.Play();
            }
        }

    }
}