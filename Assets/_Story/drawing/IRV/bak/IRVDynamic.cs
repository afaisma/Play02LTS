using UnityEngine;
using System.Collections.Generic;
using System; 

public class IRVDynamic : IRVSprite
{
    //protected GameObject m_gameObjectTarget;
    List<GameObject> m_gameObjectTarget = new List<GameObject>();

    //protected TargetBehaviour m_targetBehaviour;
    protected List<TargetBehaviour> m_targetBehaviour = new List<TargetBehaviour>();

    protected Vector3 m_vScreenPoint;
    public Vector3 m_vOriginalPosition = new Vector3();
    protected Vector3 m_vOffset;

    public bool m_bInplace = false;
    public bool m_bNInplaceIsnotPossible = false;

    public TargetBehaviour m_targetBehaviourTriggeredWith = null;
    public static Vector3 m_v3StoredScale; //was protected
    protected Vector3 m_v3ScaleZero = new Vector3(0, 0, 0); //av

    private string
        sEnteredTargetArea1 = "",
        sEnteredTargetArea2 = ""; //a dynamic object could be simultaneously in two target areas

    private bool bTouchedAnyTarget = false;
    private bool bSpriteDisableDuringInstructions = false;

    public IRVDynamic(VScene vscene, IRSprite irsprite, GameObject goParent)
        : base(vscene, irsprite, goParent)
    {
        if (irsprite.XT.Length == 0 || irsprite.XT[0] == 99999)
        {
            m_bNInplaceIsnotPossible = true;
        } 		

        m_bColliderIsNeeded = true;
    }

    override public void Load()
    {
        LoadImage();
    }

    override public void AdjustXPosition(float x)
    {
        //AV animation 12/05/2019
        Vector3 v3position = m_gameObject.transform.localPosition;
        v3position.x = x; //Debug.Log("x="+x.ToString());
        m_gameObject.transform.localPosition = v3position;
    }

    override public void AdjustZPosition()
    {
        Vector3 v3position = m_gameObject.transform.localPosition;
        v3position.z = IRVSprite.DYNAMIC_LAYERZ + m_fZOffset;
        //Debug.Log("v3position.z=" + v3position.z + ", (IRVSPRITE_COUNTER * IRVSPRITE_ZSTEP)=" + (IRVSPRITE_COUNTER * IRVSPRITE_ZSTEP));
        m_gameObject.transform.localPosition = v3position;
    }

    override public void AdjustScaleBounce(float perc)
    {
        //AV animation 12/05/2019
        float angle = 10 * perc; //smaller multiplier make period of movement longer
        float k = 1F + Mathf.Abs(Mathf.Cos(angle)) * (1 - perc);
        m_gameObject.transform.localScale = new Vector3(k * IRVDynamic.m_v3StoredScale.x,
            k * IRVDynamic.m_v3StoredScale.y, IRVDynamic.m_v3StoredScale.z);
    }

    //override public float GetZPosition(){return m_gameObject.transform.localPosition.z;}//AV 08/23/2016

    public bool InPlaceOrInPlaceIsnotPossible()
    {
        if (m_bNInplaceIsnotPossible)
            return true;
        return m_bInplace;
    }

    public override void Freeze()
    {
        //m_bInplace means that the dynamic object reached its target and cannot be moved
        m_bInplace = true;
        if (m_bNInplaceIsnotPossible )
            m_gameObject.transform.localScale = m_v3ScaleZero;    
    }

    public override void MakeSpriteTransparent(float t)
    {
        //m_bInplace means that the dynamic object reached its target and cannot be moved
        m_gameObject.transform.GetComponent<SpriteRenderer>().color = new Color(1.0f, 1.0f, 1.0f, t);
    }

    //public override void RescaleSprite(float mult)
    //{//NOT USED - IT DOES NOT WORK    AV added this to enable IRVScene to rescale the sprite
    //	//m_gameObject.transform.localScale = new Vector3(m_gameObject.transform.localScale.x*mult, m_gameObject.transform.localScale.y*mult, m_gameObject.transform.localScale.z*mult);
    //	m_gameObject.transform.localScale = new Vector3(m_v3StoredScale.x*mult, m_v3StoredScale.y*mult, m_v3StoredScale.z*mult);
    //}

    public override void OnMouseDown()
    {
        //Debug.Log("Main.bListenDoNotMoveSprites="+Main.bListenDoNotMoveSprites);//Debug.Log("OnMouseDown");
        if (Main.bListenDoNotMoveSprites)
        {
            m_gameObject.transform.localScale = new Vector3(m_v3StoredScale.x * 0.9F, m_v3StoredScale.y * 0.9F,
                m_v3StoredScale.z * 0.9F);

            bSpriteDisableDuringInstructions = true;
            return;
        } //consider varying the message: "Please do not drag during instruction" ==BAD IDEA. We could confuse a user
        else bSpriteDisableDuringInstructions = false;

        if (m_bInplace)
            return; //m_bInplace means that the dynamic object reached its target and cannot be moved           

        bTouchedAnyTarget =
            false; //AV added on 10/27/2016 to clear up touching info from the previous drag. We really do not care if user TouchedAnyTarget in the previoud drag.              
        m_vScreenPoint =
            Camera.main.WorldToScreenPoint(m_gameObject.transform.position); // Get the click location.       
        m_vOffset = m_gameObject.transform.position - Camera.main.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y,
                m_vScreenPoint.z *
                1.05F)); // Get the offset of the point inside the object.//multiply z by 1.05 to move the dragged sprite on top of other dynamic sprites

        m_gameObject.transform.localScale = new Vector3(m_v3StoredScale.x * 1.05F, m_v3StoredScale.y * 1.05F,
            m_v3StoredScale.z *
            1.05F); //increase the size of the sprite by 5% to visually create a feeling of sprite being held by hand
    }

    public override void OnMouseDrag()
    {
        if (Main.bListenDoNotMoveSprites || bSpriteDisableDuringInstructions)
            return; //Listen to instructions => do not move sprites

        if (m_bInplace)
            return; //m_bInplace means that the dynamic object reached its target and cannot be moved          

        Vector3 newScreenPoint =
            new Vector3(Input.mousePosition.x, Input.mousePosition.y,
                m_vScreenPoint.z); //Get the click location.       
        Vector3 newPosition =
            Camera.main.ScreenToWorldPoint(newScreenPoint) +
            m_vOffset; //Adjust the location by adding an offset.        
        m_dynamicImage.transform.position = newPosition; // Assign new position.
    }

    public override void OnMouseUp()
    {
        //this function reports when a dynamic object is released anywhere. 
        if (Main.bListenDoNotMoveSprites)
        {
            m_gameObject.transform.localScale = new Vector3(m_v3StoredScale.x, m_v3StoredScale.y, m_v3StoredScale.z);
            return;
        } //Listen to instructions => do not move sprites
        else bSpriteDisableDuringInstructions = false;

        if (m_bInplace) return; //m_bInplace means that the dynamic object reached its target and cannot be moved

        //INCORRECT solution -> return the dynamic object to its original location
        m_dynamicImage.EaseTo(m_vOriginalPosition);
        m_irvscene.OnIRVDynamicDropped(this, m_bInplace);
        if (this.m_irvscene.bIncorrectDrop)
            m_dynamicImage
                .PlayAudioReturn(); //Global.bSoundOn && // play the innoying "return" sound only if, in fact, there was an error. Must be called after RecordStatistics(false)//LIZA: From the visual standpoint, that sound is simply associated with the object flying back to its original place... even on accidental drags the sound should remain (it is the SMILEY that gives feedback that the answer was actually wrong - the sound is simply associated with the motion).  
        m_gameObject.transform.localScale = new Vector3(m_v3StoredScale.x, m_v3StoredScale.y, m_v3StoredScale.z);
    }

    int FindTargetBehaviour(TargetBehaviour tb)
    {
        for (int i = 0; i < m_targetBehaviour.Count; i++)
        {
            if (m_targetBehaviour[i] == tb)
            {
                //Debug.Log("nTargetIndex="+i+"; m_targetBehaviour.Count=" + m_targetBehaviour.Count);
                return i;
            }
        }

        return -1;
    }


    public override void OnTriggerEnter2D(Collider2D other)
    {
        //this function is triggered when a dynamic object enters the area of any target object
        TargetBehaviour tb = other.GetComponent<TargetBehaviour>();
        if ((tb != null) && (tb.m_irvSprite != null))
        {
            bTouchedAnyTarget =
                true; //the dynamic object reached over to some target //Debug.Log("bTouchedAnyTarget=true;");

            int nTargetIndex =
                0; //In the Laguage game we have a problem of the correct sprite entering the incorrect target 
            if (m_targetBehaviour.Count > 1)
                nTargetIndex =
                    FindTargetBehaviour(
                        tb); //In the Language game the target is caracterized by its name AND its consequitive number. The correct target has index=0. // Debug.Log("m_targetBehaviour.Count="+m_targetBehaviour.Count);

            if (tb.m_irvSprite == this && nTargetIndex == 0)
            {
                //AV added '&& nTargetIndex==0' to make sure that we entered the CORRECT target and NOT the incorrect target in the Language game whereby the correct dynamic sprite has several INCORRECT targets associated with it
                //Debug.Log("the correct dynamic object ENTERED the correct target");
                //if( m_targetBehaviourTriggeredWith == tb){} else //this is the alternative possible implementation to avoid entering the correct target twice
                m_targetBehaviourTriggeredWith = tb;
            }

            //THE IMPLEMENTATION BELOW IS TO CALCULATE HOVERING////////////////////////////////////////////////////////////
            //this.m_irsprite.Name == the dynamic sprite that is dragged
            //tb.m_irvSprite.m_irsprite.Name == the target
            if (sEnteredTargetArea1 == "")
            {
                sEnteredTargetArea1 = tb.m_irvSprite.m_irsprite.Name;
            } //remember which target area I have entered
            else if (sEnteredTargetArea2 == "")
            {
                sEnteredTargetArea2 = tb.m_irvSprite.m_irsprite.Name;
            }
            //Debug.Log("OnTriggerEnter2D; "+ "this.m_irsprite.Name="+this.m_irsprite.Name.ToString()+ "; tb.m_irvSprite.m_irsprite.Name="+tb.m_irvSprite.m_irsprite.Name.ToString());      
        }
    }

    public override void OnTriggerExit2D(Collider2D other)
    {
        //this function is triggered when a dynamic object exits the area of any target object
        //Note: Hovering is defined as time spent by a dynamic object over any target, including the correct target (but only if you leave the area of the correct target. If you hover over the correct target but never drag the dynamic object away from the correct target, hovering time is not calculated).
        //Note that hovering is not detected when there is only a single correct dynamic object (meaning that there is only one target area). Therefore hovering is never detected for dynamic games.
        TargetBehaviour tb = other.GetComponent<TargetBehaviour>();
        if ((tb != null) && (tb.m_irvSprite != null))
        {
            int nTargetIndex =
                0; //In the Laguage game we have a problem of the correct sprite entering the incorrect target 
            if (m_targetBehaviour.Count > 1)
                nTargetIndex =
                    FindTargetBehaviour(
                        tb); //In the Language game the target is caracterized by its name AND its consequitive number. The correct target has index=0. // Debug.Log("m_targetBehaviour.Count="+m_targetBehaviour.Count);

            if (tb.m_irvSprite == this && nTargetIndex == 0)
            {
                //AV added '&& nTargetIndex==0' to make sure that we entered the CORRECT target and NOT the incorrect target in the Language game whereby the correct dynamic sprite has several INCORRECT targets associated with it
                //Debug.Log("the correct dynamic object EXITED the correct target");
                m_targetBehaviourTriggeredWith = null;
            }

            //THE IMPLEMENTATION BELOW IS TO CALCULATE HOVERING////////////////////////////////////////////////////////////
            //this.m_irsprite.Name is the name of the dynamic sprite that is dragged
            //tb.m_irvSprite.m_irsprite.Name is the name of the target
            if (tb.m_irvSprite.m_irsprite.Name == sEnteredTargetArea1)
            {
                sEnteredTargetArea1 = "";
            }

            if (tb.m_irvSprite.m_irsprite.Name == sEnteredTargetArea2)
            {
                sEnteredTargetArea2 = "";
            }
            //Debug.Log("OnTriggerExit2D; "+ "this.m_irsprite.Name="+this.m_irsprite.Name.ToString()+ "; tb.m_irvSprite.m_irsprite.Name="+tb.m_irvSprite.m_irsprite.Name.ToString()+"; durHov="+durHov.TotalMilliseconds.ToString());
        }
    }

    public override void OnCollisionEnter2D(Collision2D coll)
    {
    }

    public override void OnCollisionExit2D(Collision2D coll)
    {
    }

    public override void Cleanup()
    {
        base.Cleanup();
    }
}