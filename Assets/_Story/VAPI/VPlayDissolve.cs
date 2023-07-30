using System.Collections;
using UnityEngine;

public class VPlayDissolve : MonoBehaviour
{
    public SpriteRenderer spriteDissolving;
    public float dissolveAmount;
    public float dissolveSpeed;
    [ColorUsageAttribute(true,true)]
    public Color outColor;
    [ColorUsageAttribute(true, true)]
    public Color inColor;
    private Material matDissolving;


    public float dissolveMinTime = 1f;
    public float dissolveMaxTime = 5f;

    private Coroutine dissolveInCoroutine;
    private Coroutine dissolveOutCoroutine;

    void Start()
    {
        //Debug.Log("VPlayDissolve::Start");
        matDissolving = spriteDissolving.material;
        StartCoroutine(ToggleDissolveRoutine());
    }

    void Update()
    {
        matDissolving.SetFloat("_DissolveAmount", dissolveAmount);
    }
    
    

    IEnumerator ToggleDissolveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(dissolveMinTime, dissolveMaxTime));

            if (dissolveOutCoroutine != null)
            {
                StopCoroutine(dissolveOutCoroutine);
                dissolveOutCoroutine = null;
                DissolveIn();
            }
            else if (dissolveInCoroutine != null)
            {
                StopCoroutine(dissolveInCoroutine);
                dissolveInCoroutine = null;
                DissolveOut();
            }
            else if (dissolveAmount < 0.5)
                DissolveIn();
            else
                DissolveOut();
        }
    }

    public IEnumerator DissolveOutCoroutine()
    {
        while (dissolveAmount > 0)
        {
            dissolveAmount -= Time.deltaTime * dissolveSpeed;
            matDissolving.SetColor("_DissolveColor", outColor);
            yield return null; 
        }
    }

    public IEnumerator DissolveInCoroutine()
    {
        while (dissolveAmount < 1)
        {
            dissolveAmount += Time.deltaTime * dissolveSpeed;
            matDissolving.SetColor("_DissolveColor", inColor);
            yield return null;
        }
    }
    
    public void DissolveOut()
    {
        //Debug.Log("VPlayDissolve::DissolveOut");
        if (dissolveInCoroutine != null)
        {
            StopCoroutine(dissolveInCoroutine); 
            dissolveInCoroutine = null; 
        }
        dissolveOutCoroutine = StartCoroutine(DissolveOutCoroutine());
    }
    
    public void DissolveIn()
    {
        //Debug.Log("VPlayDissolve::DissolveIn");
        if (dissolveOutCoroutine != null)
        {
            StopCoroutine(dissolveOutCoroutine);
            dissolveOutCoroutine = null;
        }
        dissolveInCoroutine = StartCoroutine(DissolveInCoroutine());
    }
}
