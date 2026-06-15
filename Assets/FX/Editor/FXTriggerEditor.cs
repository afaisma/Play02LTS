using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Optional: turns FXTrigger.effectId into a dropdown populated from the FX library
// so authors pick a valid id instead of typing a string. A plain string field
// works fine without this editor — it is pure convenience.
[CustomEditor(typeof(FXTrigger))]
public class FXTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var trigger = (FXTrigger)target;
        serializedObject.Update();

        var ids = CollectEffectIds();

        // effectId as a dropdown when ids are available, else a plain text field.
        var idProp = serializedObject.FindProperty("effectId");
        if (ids.Count > 0)
        {
            int cur = Mathf.Max(0, ids.IndexOf(trigger.effectId));
            int next = EditorGUILayout.Popup("Effect Id", cur, ids.ToArray());
            idProp.stringValue = ids[next];
        }
        else
        {
            EditorGUILayout.PropertyField(idProp, new GUIContent("Effect Id"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("trigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetRect"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("decorate"));

        serializedObject.ApplyModifiedProperties();
    }

    private static List<string> CollectEffectIds()
    {
        var ids = new List<string>();
        var lib = Resources.Load<FXLibrary>("FX/FXLibrary");
        if (lib == null) lib = FXLibrary.CreateDefault();
        if (lib.effects != null)
            foreach (var e in lib.effects)
                if (e != null && !string.IsNullOrEmpty(e.id)) ids.Add(e.id);
        return ids;
    }
}
