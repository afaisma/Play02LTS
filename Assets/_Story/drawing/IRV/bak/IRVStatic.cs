using UnityEngine;

public class IRVStatic : IRVSprite
{
    public IRVStatic(VScene vscene, IRSprite irsprite, GameObject goParent) : base(vscene, irsprite, goParent)
    {
    }

    override public void AdjustZPosition()
    {
        Vector3 v3position = m_gameObject.transform.localPosition;
        v3position.z = IRVSprite.STATIC_LAYERZ + m_fZOffset;
        m_gameObject.transform.localPosition = v3position;
        if (m_irsprite.xt == 88888) 
        {
            Global.targetZcoordinate = v3position.z - 0.01F; 
        }
    }

    //override public float GetZPosition(){Debug.Log("Inside IRVStatic.cs>GetZPosition. Z = "+m_gameObject.transform.localPosition.z); return m_gameObject.transform.localPosition.z;}//AV 08/23/2016

    override public void AdjustXPosition(float x)
    {
        //AV cars animation 10/13/2015
        Vector3 v3position = m_gameObject.transform.localPosition;
        v3position.x = x; //Debug.Log("x="+x.ToString());
        m_gameObject.transform.localPosition = v3position;
    }

    override public void AdjustYPosition(float y)
    {
        //AV bugs animation 10/20/2015
        Vector3 v3position = m_gameObject.transform.localPosition;
        v3position.y = y;
        m_gameObject.transform.localPosition = v3position;
    }

    override public void AdjustRotation(float angle)
    {
        //AV bugs animation 10/20/2015
        //this is what I wanted: it rotates the sprite 45 degrees around Z axis (the front of the car is lifted us by 45 degrees)
        //the only problem is that it is always rotated around the left bottom corner of the image and I don't think it is easy to change
        Vector3 v3rot = new Vector3(0, 0, angle);
        m_gameObject.transform.localRotation = Quaternion.Euler(v3rot);

        //this rotates the sprite 45 degrees around Y axis (it is like a tower of Hanoy: the Y axis penetrates the car and holds it)
        //Vector3 v3rot= new Vector3(0,angle,0);
        //m_gameObject.transform.localRotation = Quaternion.Euler(v3rot);

        //this rotates the sprite 45 degrees backwars (wheels are solid on the road, attached to X axis)
        //Vector3 v3rot= new Vector3(angle,0,0);
        //m_gameObject.transform.localRotation = Quaternion.Euler(v3rot);

        //this created illusion of wings going up and down
        //Vector3 v3rot= new Vector3(angle,0,0);
        //m_gameObject.transform.localRotation = m_gameObject.transform.localRotation * Quaternion.Euler(v3rot);
    }

    override public void AdjustScaleBounce(float perc)
    {
        //AV animation 03/28/2016
        float angle = 10 * perc; //smaller multiplier make period of movement longer
        float k = 1F + Mathf.Abs(Mathf.Cos(angle)) * (1 - perc);
        m_gameObject.transform.localScale = new Vector3(k * IRVDynamic.m_v3StoredScale.x,
            k * IRVDynamic.m_v3StoredScale.y, IRVDynamic.m_v3StoredScale.z);
    }

    override public void AdjustScaleLinear(float perc)
    {
        //AV animation 05/14/2018
        float k = perc; //*perc;
        m_gameObject.transform.localScale = new Vector3(k * IRVDynamic.m_v3StoredScale.x,
            k * IRVDynamic.m_v3StoredScale.y, IRVDynamic.m_v3StoredScale.z);
        //Debug.Log(perc.ToString());
    }

    public override void MakeSpriteTransparent(float t)
    {
        //m_bInplace means that the dynamic object reached its target and cannot be moved
        m_gameObject.transform.GetComponent<SpriteRenderer>().color = new Color(1.0f, 1.0f, 1.0f, t);
    }
}