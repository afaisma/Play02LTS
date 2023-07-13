using UnityEngine;

public class IRVBackground : IRVSprite
{
    public IRVBackground(VScene vscene, IRSprite irsprite, GameObject goParent) : base(vscene, irsprite, goParent)
    {
    }

    override public void AdjustZPosition()
    {
        Vector3 v3position = m_gameObject.transform.localPosition;
        v3position.z = IRVSprite.BACKGROUND_LAYERZ + m_fZOffset;
        m_gameObject.transform.localPosition = v3position;
    }

    //override public float GetZPosition(){return m_gameObject.transform.localPosition.z;}//AV 08/23/2016

    override public void Cleanup()
    {
        if (m_dynamicImage != null)
        {
            m_dynamicImage.Cleanup(true);
            m_dynamicImage = null;
        }
    }
}