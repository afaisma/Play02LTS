using UnityEngine;
using System.Collections;
using DG.Tweening;

[RequireComponent(typeof(AudioSource))]
public class DynamicImageBehaviour : MonoBehaviour
{
	private static float ZStep = -0.05f; ///-1.01f;
	private static float ZLast = 0.0f;

	private Sprite m_sprite;
    public IRVSprite m_irvSprite;

    public AudioClip audioClipReturn;
    public AudioClip audioClipSuction;
    
    void Start()
    {
        //audioClipReturn = (AudioClip)Main.InstantiateObject(Resources.Load("Audio/Explosion", typeof(AudioClip)));
        //audioClipSuction = (AudioClip)Main.InstantiateObject(Resources.Load("Audio/suction", typeof(AudioClip)));
    }

    public void LoadImageFromURLWrapper(string url, Vector3 position, float scalex, float scaley, System.Action<bool> aCallback, bool bColliderIsNeeded) 
    {
	    StartCoroutine(LoadImageFromURL(url, position, scalex, scaley, aCallback, bColliderIsNeeded));
    }


    public IEnumerator LoadImageFromURL(string url, Vector3 position, float scalex, float scaley, System.Action<bool> aCallback, bool bColliderIsNeeded)
    {
        WWW www = new WWW (url);
		yield return www;

		if (www.error == null)  
		{
			SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
			m_sprite = Sprite.Create(www.texture, new Rect(0, 0, www.texture.width, www.texture.height),new Vector2(0, 0), 100.0f);

			renderer.sprite = m_sprite;
            renderer.sprite.texture.wrapMode = TextureWrapMode.Clamp;
             
			ZLast = ZLast + ZStep;
			renderer.transform.localPosition = new Vector3(position.x + 0f, position.y, position.z);
			renderer.transform.localScale = new Vector3(1, 1, 1);
            if (bColliderIsNeeded) 
            {
                BoxCollider2D boxCollider2D = gameObject.AddComponent<BoxCollider2D>();
                boxCollider2D.isTrigger = true;
				if(boxCollider2D.size.x<=0.3) boxCollider2D.size = new Vector2(2*boxCollider2D.size.x, boxCollider2D.size.y);//we want to call these AFTER we sort  sprites by their size
				if(boxCollider2D.size.y<=0.3) boxCollider2D.size = new Vector2(boxCollider2D.size.x, 2*boxCollider2D.size.y);
            }
			if (aCallback != null) aCallback(true);
		} 
		else 
		{
			Debug.Log(www.error + "url=" + url);
			if (aCallback != null) aCallback(false);
		}
       
        www.Dispose();
        www = null;
	}

    public void SetCollder()
    {
        //renderer.material = defaultMaterial;	
        BoxCollider2D boxCollider2D = gameObject.AddComponent<BoxCollider2D>();
        boxCollider2D.isTrigger = true;
    }

    public void Cleanup(bool bDestroySprite)
    {
        if (m_sprite != null)
        {
            if (m_sprite.texture != null)
            {
				if (bDestroySprite)
	                Destroy(m_sprite.texture);
            }
			if (bDestroySprite)
	            Sprite.Destroy(m_sprite);
            m_sprite = null;
        } 
    }

    void OnMouseDown()
	{
        //Debug.Log("DynamicImageBehaviour::OnMouseDown()");
        m_irvSprite.OnMouseDown();
	}
 
	void OnMouseDrag ()
	{
        //Debug.Log("DynamicImageBehaviour::OnMouseDrag()");
        m_irvSprite.OnMouseDrag();
	}

    void OnMouseUp()
    {
        //Debug.Log("DynamicImageBehaviour::OnMouseUp()");
        m_irvSprite.OnMouseUp();
	} 

    public void EaseTo(Vector3 position)
    {
        //StartCoroutine(transform.MoveTo(position, 0.25f, Ease.Custom));
        //StartCoroutine(transform.MoveTo(position, 0.25f, Ease.Custom));
        transform.DOMove(position, 0.25f).SetEase(Ease.OutQuad); 
    }

    void OnTriggerEnter2D(Collider2D other)
    {
		if (m_irvSprite != null) m_irvSprite.OnTriggerEnter2D(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
		if (m_irvSprite != null) m_irvSprite.OnTriggerExit2D(other);
    }

    void OnCollisionEnter2D(Collision2D coll) 
    {
		if (m_irvSprite != null) m_irvSprite.OnCollisionEnter2D(coll);
    }

    void OnCollisionExit2D(Collision2D coll)
    {
		if (m_irvSprite != null) m_irvSprite.OnCollisionExit2D(coll);
    }

    public void PlayAudioReturn()
    {
        AudioSource.PlayClipAtPoint(audioClipReturn, Camera.main.transform.position);
    }

    public void PlayAudioSuction()
    {
        AudioSource.PlayClipAtPoint(audioClipSuction, Camera.main.transform.position);
    }


}