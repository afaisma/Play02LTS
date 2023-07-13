using UnityEngine;

public class IRVSprite
{
    public static float BACKGROUND_LAYERZ = 5.0f;
    public static float STATIC_LAYERZ = 2.5f;
    public static float DYNAMIC_LAYERZ = -2.5f;
    public static float SMILIK_LAYERZ = -7.5f;
    public static int ZADJUSTMENT_COUNTER = 0;

    public static float IRVSPRITE_ZSTEP = -0.02f; //minus indicates being closer to the camera.
    protected VScene m_irvscene;
    public IRSprite m_irsprite;
    protected GameObject m_gameObject;
    protected DynamicImageBehaviour m_dynamicImage;
    protected AudioSource m_audioSource;
    protected GameObject m_goparent;

    protected bool m_bColliderIsNeeded = false;
    protected float m_fZOffset = 0;

    public IRVSprite(VScene irvscene, IRSprite irsprite, GameObject parent)
    {
        m_irvscene = irvscene;
        m_irsprite = irsprite;
        m_goparent = parent;

        //m_gameObject = Main.createVGameObject();
        //irvscene.StartCoroutine(PRUtils.DownloadSprite(irsprite.GetFileName()));
        m_gameObject = Main.CreateFullScreenGameObject(null);
        m_dynamicImage = m_gameObject.GetComponent<DynamicImageBehaviour>();
        m_gameObject.transform.parent = parent.transform;
        m_audioSource = (AudioSource)m_gameObject.GetComponent(typeof(AudioSource));

        if (irsprite.Type != Global.TYPE_BACKGROUND)
        {
            //this hack was added for static games. In the XML, we list bigger sprites first and smallest sprites last. That way bigger objects are displayed first and smaller objects are displayed on top. This ensures that user can see small objects, even if there is an overlap           
            m_fZOffset = (ZADJUSTMENT_COUNTER * IRVSPRITE_ZSTEP);
            ZADJUSTMENT_COUNTER++;
        }
    }

    bool bLoadSpritesFromStreamingAssetsFolder = true; //bOldWay

    virtual public void LoadImage()
    {
        Vector3 v3position = Main.GetWorldPosFromVirtualScreenXY(m_irsprite.X, m_irsprite.Y);
        v3position.z = BACKGROUND_LAYERZ;

        if (true)
        {
            //m_dynamicImage.LoadImageFromStreamingAssetsWrapper(ResolveFileName(m_irsprite.getFileName()), v3position, Global.ScXGlobal * m_irsprite.ScX, Global.ScYGlobal * m_irsprite.ScY, (success) =>
            m_dynamicImage.LoadImageFromURLWrapper(m_irsprite.GetFileName(), v3position,
                Global.ScXGlobal * m_irsprite.ScX, Global.ScYGlobal * m_irsprite.ScY, (success) =>
                {
                    //this function load all sprites, not only dynamic sprites
                    if (success)
                    {
                        AdjustZPosition();
                        m_dynamicImage.m_irvSprite = this;
                    }
                },
                m_bColliderIsNeeded);
        }
    }

    virtual public void Cleanup()
    {
        if (m_dynamicImage != null)
        {
            m_dynamicImage.Cleanup(true);
            m_dynamicImage = null;
        }

        m_irsprite.Cleanup();
    }

    virtual public void Load()
    {
        LoadImage();
    }


    virtual public void AdjustZPosition()
    {
    }

    //virtual public float GetZPosition(){return 0F;}//AV 08/23/2016
    virtual public void AdjustXPosition(float x)
    {
    } //AV cars animation 10/13/2015

    virtual public void AdjustYPosition(float y)
    {
    } //AV bugs animation 10/28/2015

    virtual public void AdjustRotation(float angle)
    {
    } //AV bugs animation 10/28/2015 

//  virtual public void AdjustXRotation(float x){}//AV bugs animation 10/28/2015
//  virtual public void AdjustYRotation(float y){}//AV bugs animation 10/28/2015
    virtual public void AdjustScaleBounce(float perc)
    {
    } //AV cars animation 10/13/2015

    virtual public void AdjustScaleLinear(float perc)
    {
    }

    virtual public void Freeze()
    {
    }

    virtual public void MakeSpriteTransparent(float t)
    {
    }

    virtual public void OnMouseDown()
    {
    }

    virtual public void OnMouseDrag()
    {
    }

    virtual public void OnMouseUp()
    {
    }

    virtual public void OnTriggerEnter2D(Collider2D other)
    {
    }

    virtual public void OnTriggerExit2D(Collider2D other)
    {
    }

    virtual public void OnCollisionEnter2D(Collision2D coll)
    {
    }

    virtual public void OnCollisionExit2D(Collision2D coll)
    {
    }
//	virtual public void RescaleSprite(float mult){ }//AV added this to enable IRVScene to rescale the sprite
}