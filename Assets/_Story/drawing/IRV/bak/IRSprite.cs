using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class IRSprite
{
    public int SpriteOrderIndex = 0;
    
    public string Name;
    public string Type;
    public string File;
    public float X;
    public float Y;
    public float XF = -1f;
    public float YF = -1f;
    public float xt;
    public float yt;
    public float ScX = 1.0f;
    public float ScY = 1.0f;
    public float targetscale = 1.0f;

    float[] XXTT = new float[0];
    float[] YYTT = new float[0];

    public float[] XT
    {
        get
        {
            InitTargetsList();
            return XXTT;
        }
    }

    public float[] YT
    {
        get
        {
            InitTargetsList();
            return YYTT;
        }
    }

    public string GetFileName()
    {
        return File;
    }

    public void InitTargetsList()
    {
        if (XXTT.Length == 0 && xt != 0)
        {
            XXTT = new float[] { xt };
        }

        if (YYTT.Length == 0 && yt != 0)
        {
            YYTT = new float[] { yt };
        }
    }

    public void AddTarget(float xt, float yt)
    {
        List<float> listXT = new List<float>(XT);
        List<float> listYT = new List<float>(YT);
        
        listXT.Add(xt);
        listYT.Add(yt);

        XXTT = listXT.ToArray();
        YYTT = listYT.ToArray();
    }

    public void Cleanup()
    {
        XXTT = new float[0];
        YYTT = new float[0];
    }

    public static void ExampleSubsystem()
    {
        string jsonString = "";
        IRSprite irSprite = JsonUtility.FromJson<IRSprite>(jsonString);

        // or if you have an array of IRSprite in the JSON
        IRSpriteListWrapper irSpriteList = JsonUtility.FromJson<IRSpriteListWrapper>(jsonString);
        List<IRSprite> irSprites = new List<IRSprite>(irSpriteList.irSprites);
    }
}

[Serializable]
public class IRSpriteListWrapper
{
    public IRSprite[] irSprites;
}