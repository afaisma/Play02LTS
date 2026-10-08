using System.Collections;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// The system's microphone permission, asked the way each system wants it: Android has its own
/// permission calls; iOS (and the Editor) use Unity's user authorization. The speech recognizer
/// asks in the same two ways (Estrada's DefaultMicrophoneController), so both always agree.
/// </summary>
public static class DialogueMicPermission
{
    public static bool Granted
    {
        get
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Application.platform == RuntimePlatform.Android)
                return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#endif
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
        }
    }

    /// <summary>Shows the system question (if the system still shows it) and waits for the answer.</summary>
    public static IEnumerator Request()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (Application.platform == RuntimePlatform.Android)
        {
            bool pending = true;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => pending = false;
            callbacks.PermissionDenied += _ => pending = false;
            callbacks.PermissionDeniedAndDontAskAgain += _ => pending = false;
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
            // The answer normally comes through the callbacks. The time limit is a safety net: a
            // book must never hang on a system dialog that did not report back.
            float deadline = Time.realtimeSinceStartup + 60f;
            while (pending && Time.realtimeSinceStartup < deadline) yield return null;
            yield break;
        }
#endif
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
    }
}
