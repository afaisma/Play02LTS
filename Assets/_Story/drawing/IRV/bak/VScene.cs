using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFSW.QC;

public class VScene : MonoBehaviour
{
    List<IRVSprite> lsIrvsprites = new List<IRVSprite>(); 
    public GameObject m_gameObjectRoot;
    private Vector3 m_v3storedPosition;
    public int nTargetObjects = 0;
    public bool bIncorrectDrop = false;

    private static string sBackgroundSprite;
    private static int[,] staticSpriteCoord;
    private static int[,] targetSpriteCoord;

    //we set sprites coordinates from zero to 2048
    private static float leftStartCoord = -230f, rightStartCoord = 2048f;

    public void AddBackground()
    {
        IRSprite irsprite = new IRSprite();
        irsprite.Name = "background";
        irsprite.File = "http://localhost:8080/api/files/download/stories/defaultImages/Bg1.jpg";
        irsprite.Type = Global.TYPE_BACKGROUND;
        IRVSprite irvSprite = new IRVBackground(this, irsprite, m_gameObjectRoot);
        irvSprite.Load();
        lsIrvsprites.Add(irvSprite);
    }
    
    public void AddDynamic()
    {
        IRSprite irsprite = new IRSprite();
        irsprite.Name = "dynamic";
        irsprite.File = "http://localhost:8080/api/files/download/stories/defaultImages/Bg1.jpg";
        irsprite.Type = Global.TYPE_DYNAMIC;
        IRVSprite irvSprite = new IRVDynamic(this, irsprite, m_gameObjectRoot);
        irvSprite.Load();
        lsIrvsprites.Add(irvSprite);
    }
    
    public void _IRVScene(IRScene irScene)
    {
        IRVSprite.ZADJUSTMENT_COUNTER = 0; //z-axis adustment for all sprites except background
        if (GameObject.Find("GameObjectSceneRoot(Clone)") == null) //Global.bGameType[Global.currentGameIndex]==0
            m_gameObjectRoot =
                (GameObject)Main.InstantiateObject(Resources.Load("GameObjectSceneRoot", typeof(GameObject)));
        else //in dynamic games we don't kill the GameObjectSceneRoot(Clone) and therefore we don't need to instantiate a new game object
            m_gameObjectRoot = GameObject.Find("GameObjectSceneRoot(Clone)");
        m_v3storedPosition = new Vector3(m_gameObjectRoot.transform.position.x, m_gameObjectRoot.transform.position.y,
            m_gameObjectRoot.transform.position.y);
        m_gameObjectRoot.transform.position = new Vector3(1000, 0, 0);
        IRSprite irsprite;
        IRVSprite irvSprite = null;
        int i;

        if (irScene != null)
        {
            for (i = 0; i < irScene.maIRSprites.Length; i++)
            {
                //set filenames from XML
                irsprite = irScene.maIRSprites[i]; //holds data read from XML

                if (irsprite.Type == Global.TYPE_BACKGROUND)
                {
                    irvSprite = new IRVBackground(this, irsprite, m_gameObjectRoot);
                }
                else if (irsprite.Type == Global.TYPE_DYNAMIC)
                {
                    irvSprite = new IRVDynamic(this, irsprite, m_gameObjectRoot);
                }
                else if (irsprite.Type == Global.TYPE_STATIC)
                {
                    irvSprite = new IRVStatic(this, irsprite, m_gameObjectRoot);
                }

                lsIrvsprites.Add(irvSprite);
            }
        } //END OF simple static game read from XML like Pooza

        //we finished setting the filenames, now load images
        foreach (IRVSprite irvSpr in lsIrvsprites)
        {
            irvSpr.Load(); //z position is adjusted here after the sprite has been loaded. However the images are loaded asynchronously. Thus I cannot ask for z position here.			
        }
    }

    public void Show()
    {
        m_gameObjectRoot.transform.position = m_v3storedPosition;
    }

    public void OnIRVDynamicDropped(IRVDynamic irvdDynamic, bool bInPlace)
    {
        IRVSprite irvsprite;
        bool bCompleted = true;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            //check if all dynamic objects are in place?
            irvsprite = lsIrvsprites[i];

            if (irvsprite.m_irsprite.Type == Global.TYPE_DYNAMIC)
            {
                if (!(irvsprite as IRVDynamic).InPlaceOrInPlaceIsnotPossible())
                {
                    bCompleted = false;
                    break; //not all dynamic objects are in place
                }
            }
        }

        if (bInPlace && bCompleted)
        {
            //the puzzle was completed
            Freeze();
        } //if (bInPlace && bCompleted)
    }
    
    public void Freeze()
    {
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            irvsprite.Freeze();
        }
    }

    public void Cleanup()
    {
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if ( irvsprite.m_irsprite.Type != Global.TYPE_BACKGROUND)
            {
                //in static game --> kill all sprites; //in a dynamic game kill everything but the background that does not change between games
                irvsprite.Cleanup(); //Debug.Log("called irvsprite.Cleanup();");
                lsIrvsprites
                    .Remove(irvsprite); //AV 11/04/2015 I cannot use this. For some reason the dynamic sprites are shown one on top of the other
            }
        }
        //lsIrvsprites.Clear();//AV 10/29/15 For some reason the background does not disappear even when I use .Clear(), but better be safe.
    }

    public void MakeDynamicSpritesTransparent(float t)
    {
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if (irvsprite.m_irsprite.Type == Global.TYPE_DYNAMIC) irvsprite.MakeSpriteTransparent(t);
        }
    }

    public void MakeStaticSpritesTransparent(float t)
    {
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if (irvsprite.m_irsprite.Type == Global.TYPE_STATIC) irvsprite.MakeSpriteTransparent(t);
        }
    }

    public void Make_focusOnListening_SpriteTransparent(float t)
    {
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if (irvsprite.m_irsprite.Name == "focus_on_listening") irvsprite.MakeSpriteTransparent(t);
        }
    }

    //LERPING FUNCTIONS///////////////////////////////////////////////////////////////
    public void LerpCar_SpriteX(float timeRatioX, string sStaticOrDynamic)
    {
        //AV 12/05/2019. Since sprite positions are defined in this script, I have to set the position of the animated sprite (the car) here
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if (irvsprite.m_irsprite.Type == sStaticOrDynamic)
            {
                //now we have to find the sprite to animate. in the case of the Car game, it is easy. The animated car is the only static sprite
                float
                    tx; //see examples of nonlinear lerping here: https://chicounity3d.wordpress.com/2014/05/23/how-to-lerp-like-a-pro/
                //tx = timeRatioX;//linear movement
                //tx = Mathf.Sin(timeRatioX * Mathf.PI * 0.5f);//�ease out� with sinerp = fast then slow down
                //tx = 1f - Mathf.Cos(timeRatioX * Mathf.PI * 0.5f); //�ease in� with coserp = slow then increase speed
                //tx = timeRatioX * timeRatioX;//exponential movement = slow then increase speed
                tx = timeRatioX * timeRatioX * (3f - 2f * timeRatioX); //Smoothstep - best for the car moving in
                //tx = timeRatioX * timeRatioX * timeRatioX * (timeRatioX * (6f * timeRatioX - 15f) + 10f);//Smootherstep
                float
                    x = Mathf.Lerp(irvsprite.m_irsprite.X, irvsprite.m_irsprite.XF,
                        tx); //float x = Mathf.Lerp(spriteFromPosX, irvsprite.m_irsprite.X, tx);//entering the scene //Debug.Log("x="+x.ToString());
                Vector3 v3position = Main.GetWorldPosFromVirtualScreenXY(x, 0);
                irvsprite.AdjustXPosition(v3position.x); //Debug.Log("x="+v3position.x.ToString());
                //break; comment out breat to animate ALL dynamic sprites
            }
        }
    }

    public void LerpWalking_SpriteX(float timeRatioX, string sStaticOrDynamic)
    {
        //walking imitation-uses sinusoid//AV 12/05/2019. Since sprite positions are defined in this script, I have to set the position of the animated sprite (the car) here
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if (irvsprite.m_irsprite.Type == sStaticOrDynamic)
            {
                //now we have to find the sprite to animate. in the case of the Car game, it is easy. The animated car is the only static sprite
                float
                    tx = timeRatioX; // * Mathf.Abs(Mathf.Cos(2 * Mathf.PI * 3*timeRatioX));//1 + animAmplit * amplitudeScalar * Mathf.Sin(2 * Mathf.PI * current_AnimTime / animPeriod);//linear movement
                float
                    x = Mathf.Lerp(irvsprite.m_irsprite.X, irvsprite.m_irsprite.XF,
                        tx); //float x = Mathf.Lerp(spriteFromPosX, irvsprite.m_irsprite.X, tx);//entering the scene //Debug.Log("x="+x.ToString());
                Vector3 v3position = Main.GetWorldPosFromVirtualScreenXY(x, 0);
                irvsprite.AdjustXPosition(v3position.x); //Debug.Log("x="+v3position.x.ToString());
                irvsprite.AdjustScaleBounce(1 + 0.15f * Mathf.Abs(Mathf.Sin(2 * Mathf.PI * 2 * timeRatioX)));
                //break; comment out breat to animate ALL dynamic sprites
            }
        }
    }

    public void LerpInsect_SpriteX(float timeRatioX, string sStaticOrDynamic)
    {
        //Insects are moving slowly, then fast//AV 12/05/2019. Since sprite positions are defined in this script, I have to set the position of the animated sprite (the car) here
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if (irvsprite.m_irsprite.Type == sStaticOrDynamic)
            {
                //now we have to find the sprite to animate. in the case of the Car game, it is easy. The animated car is the only static sprite
                float
                    tx = timeRatioX *
                         timeRatioX; //exponential movement = slow then increase speed//see examples of nonlinear lerping here: https://chicounity3d.wordpress.com/2014/05/23/how-to-lerp-like-a-pro/
                float
                    x = Mathf.Lerp(irvsprite.m_irsprite.X, irvsprite.m_irsprite.XF,
                        tx); //float x = Mathf.Lerp(spriteFromPosX, irvsprite.m_irsprite.X, tx);//entering the scene //Debug.Log("x="+x.ToString());
                Vector3 v3position = Main.GetWorldPosFromVirtualScreenXY(x, 0);
                irvsprite.AdjustXPosition(v3position.x); //Debug.Log("x="+v3position.x.ToString());
            }
        }
    }

    public void BounceSprite(float perc, string sStaticOrDynamic)
    {
        //AV 03/28/2016. Since sprite positions and scale are defined in this script, I have to set the scale of the animated sprite here
        //change the sprite's size from large to normal//AV 03/28/2016. Since sprite positions and scale are defined in this script, I have to set the scale of the animated sprite here
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];
            if (irvsprite.m_irsprite.Type == sStaticOrDynamic)
            {
                //now we have to find the sprite to animate. in the case of the wooden, it is easy. The animated car is the only static sprite
                irvsprite.AdjustScaleBounce(perc);
                //break;
            }
        }
    }

    public void ScaleSpriteUpLinearly(float perc, string sStaticOrDynamic)
    {
        //AV 03/28/2016. Since sprite positions and scale are defined in this script, I have to set the scale of the animated sprite here
        IRVSprite irvsprite;
        for (int i = 0; i < lsIrvsprites.Count; i++)
        {
            irvsprite = lsIrvsprites[i];

            if (irvsprite.m_irsprite.Type == sStaticOrDynamic)
            {
                //now we have to find the sprite to animate. in the case of the wooden, it is easy. The animated car is the only static sprite
                irvsprite.AdjustScaleLinear(perc);
                break;
            }
        }
    }

    //to fix top right simply assign coordinated. No conversion is necessaty
    private float FixLeft__ConvertCoordinateX(float top_left)
    {
        return top_left;
    }

    private float FixTop___ConvertCoordinateY(float top_left)
    {
        return top_left;
    }

    private float FixRight_ConvertCoordinateX(float top_left, int width, float ScaleDownFactor)
    {
        return top_left - width * (Global.ScXGlobal) * ScaleDownFactor;
    }

    private float FixBottomConvertCoordinateY(float top_left, int hight, float ScaleDownFactor)
    {
        return top_left - hight * (Global.ScYGlobal) * ScaleDownFactor;
    }

    private float FixCenterConvertCoordinateX(float top_left, int width, float ScaleDownFactor)
    {
        return top_left - width * (Global.ScXGlobal / 2) * ScaleDownFactor;
    }

    private float FixCenterConvertCoordinateY(float top_left, int hight, float ScaleDownFactor)
    {
        return top_left - hight * (Global.ScYGlobal / 2) * ScaleDownFactor;
    }
}
