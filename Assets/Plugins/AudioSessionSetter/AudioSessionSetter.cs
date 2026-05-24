using System.Runtime.InteropServices;
using UnityEngine;
 
namespace Imagiration.Plugins
{
    public class AudioSessionSetter : MonoBehaviour
    {
 
        // -------------------------------------------------------------------------
        // MonoBehaviour Calls
        // -------------------------------------------------------------------------
 
        private void Awake()
        {
#if !UNITY_EDITOR
    #if UNITY_IOS
                SetAudioSession();
    #endif
#endif
        }
 
 
 
        // -------------------------------------------------------------------------
        // Native Code Calls
        // -------------------------------------------------------------------------
 
#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void _SetAudioSession();
 
        // -------------------------------------------------------------------------
        public static void SetAudioSession()
        {
            _SetAudioSession();//this script is used to to disable silent mode on iOS
        }
#else
        // -------------------------------------------------------------------------
        public static void SetAudioSession()
        {
            //not implemented --> fallback
        }
#endif
    }
}