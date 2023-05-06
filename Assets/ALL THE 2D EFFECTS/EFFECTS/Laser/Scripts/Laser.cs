using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Grogan.Effects2D
{

    public class Laser : MonoBehaviour
    {
        [SerializeField] bool showStartEffects;
        [SerializeField] bool showEndEffects;

        [SerializeField] bool isOn;
        public bool IsOn
        {
            get
            {
                return isOn;
            }
        }
        [SerializeField] Material beamMaterial;
        [SerializeField] Material flashMaterial;

        [SerializeField] Material particlesMaterial;

        [SerializeField] ParticleSystem startFlash;
        [SerializeField] ParticleSystem startParticles;
        [SerializeField] ParticleSystem endFlash;
        [SerializeField] ParticleSystem endParticles;
        [SerializeField] LineRenderer beam;

        [SerializeField] Transform startEffectTransform;
        [SerializeField] Transform endEffectTransform;

        [SerializeField] Transform defaultStartPos;
        [SerializeField] Transform defaultEndPos;

        void Awake()
        {
            if (defaultEndPos != null && defaultStartPos != null) beam.SetPositions(new Vector3[] { defaultStartPos.localPosition, defaultEndPos.localPosition });
        }
        private void Start()
        {
            SetMaterials();
            SetBeamState(isOn);
        }

        private void Update()
        {
            if (isOn)
            {
                if (showStartEffects)
                {
                    startEffectTransform.position = beam.GetPosition(0);
                }
                else
                {
                    startFlash.Stop();
                    startParticles.Stop();
                }
                if (showEndEffects)
                {
                    endEffectTransform.position = beam.GetPosition(1);
                }
                else
                {
                    endFlash.Stop();
                    endParticles.Stop();
                }
            }
        }

        public void PowerBeam(bool on)
        {
            SetBeamState(on);
        }

        public void SetBeamPoints(Vector2 startPos, Vector2 endPos)
        {
            beam.SetPosition(0, startPos);
            beam.SetPosition(1, endPos);
        }

        void SetBeamState(bool on)
        {
            isOn = on;
            beam.enabled = on;
            if (on)
            {
                startFlash.Play();
                endFlash.Play();
                startParticles.Play();
                endParticles.Play();
            }
            else
            {
                startFlash.Stop();
                endFlash.Stop();
                startParticles.Stop();
                endParticles.Stop();
            }
        }

        void SetMaterials()
        {
            beam.material = beamMaterial;

            if (showStartEffects)
            {
                startFlash.GetComponent<ParticleSystemRenderer>().material = flashMaterial;
                startParticles.GetComponent<ParticleSystemRenderer>().material = particlesMaterial;
            }
            if (showEndEffects)
            {
                endFlash.GetComponent<ParticleSystemRenderer>().material = flashMaterial;
                endParticles.GetComponent<ParticleSystemRenderer>().material = particlesMaterial;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SetMaterials();
            if (defaultEndPos != null && defaultStartPos != null) beam.SetPositions(new Vector3[] { defaultStartPos.position, defaultEndPos.position });
        }

#endif
    }
}
