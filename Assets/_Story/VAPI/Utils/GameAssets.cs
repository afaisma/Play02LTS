/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */
 
using UnityEngine;
using System.Reflection;

public class GameAssets : MonoBehaviour {

    private static GameAssets _i;

    public static GameAssets i {
        get {
            if (_i == null) _i = Instantiate(Resources.Load<GameAssets>("GameAssets"));
            return _i;
        }
    }


    public SoundAudioClip[] soundAudioClipArray;

    [System.Serializable]
    public class SoundAudioClip {
        public SoundManager.Sound sound;
        public AudioClip audioClip;
    }





    #region Default
    [HideInInspector]public Sprite codeMonkeyHeadSprite;

    [HideInInspector]public Sprite s_ShootFlash;
    
    [HideInInspector]public Transform pfSwordSlash;
    [HideInInspector]public Transform pfEnemy;
    [HideInInspector]public Transform pfEnemyFlyingBody;

    [HideInInspector]public Material m_WeaponTracer;
    [HideInInspector]public Material m_MarineSpriteSheet;
    #endregion






}
