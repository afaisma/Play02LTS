using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{

    public class Fog : MonoBehaviour
    {
        [SerializeField] bool enableBackgroundFog = true;
        [SerializeField] bool enableBlanketFog = true;
        [SerializeField] float blanketDensity = 5f;
        float defaultDensity = 10f;

        [Header("References")]
        [SerializeField] GameObject backgroundFog;
        [SerializeField] ParticleSystem blanketFog;


        void Update()
        {
            if (backgroundFog != null && enableBackgroundFog && !backgroundFog.activeSelf)
            {
                backgroundFog.SetActive(true);
            }
            else if (backgroundFog != null && !enableBackgroundFog && backgroundFog.activeSelf)
            {
                backgroundFog.SetActive(false);
            }

            if (blanketFog != null && enableBlanketFog && !blanketFog.isPlaying)
            {

                blanketFog.Play();
            }
            else if (blanketFog != null && !enableBlanketFog && blanketFog.isPlaying)
            {
                blanketFog.Stop();
            }

            if (blanketFog != null && blanketFog.isPlaying)
            {
                var em = blanketFog.emission;
                em.rateOverTimeMultiplier = blanketDensity * defaultDensity;
            }
        }
    }
}