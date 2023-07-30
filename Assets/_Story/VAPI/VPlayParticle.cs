using UnityEngine;
using System.Collections;

public class VPlayParticle : MonoBehaviour
{
    public ParticleSystem particle;

    [Range(0, 10)] // adjust range as needed
    public float minStartTime = 1f;
    [Range(0, 10)] // adjust range as needed
    public float maxStartTime = 5f;

    [Range(0, 10)] // adjust range as needed
    public float minPlayTime = 1f;
    [Range(0, 10)] // adjust range as needed
    public float maxPlayTime = 5f;

    [Range(0, 10)] // adjust range as needed
    public float minStopTime = 1f;
    [Range(0, 10)] // adjust range as needed
    public float maxStopTime = 5f;

    void Start()
    {
        StartCoroutine(PlayParticleRoutine());
    }

    IEnumerator PlayParticleRoutine()
    {
        // initial wait before starting particle system
        yield return new WaitForSeconds(Random.Range(minStartTime, maxStartTime));
        
        while (true)
        {
            // start the particle system
            particle.Play();

            // wait for a random duration of play time
            yield return new WaitForSeconds(Random.Range(minPlayTime, maxPlayTime));

            // stop the particle system
            particle.Stop();

            // wait for a random duration of stop time
            yield return new WaitForSeconds(Random.Range(minStopTime, maxStopTime));
        }
    }
}