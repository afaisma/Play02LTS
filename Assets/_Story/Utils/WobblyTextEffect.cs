using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class WobblyTextEffect : MonoBehaviour
{
    private TMP_Text tmpText;
    public float wobbleSpeed = 5.0f;
    public float wobbleAmount = 0.1f;

    private void Awake()
    {
        tmpText = GetComponent<TMP_Text>();
    }

    private void LateUpdate()
    {
        WobbleText();
    }

    private void WobbleText()
    {
        tmpText.ForceMeshUpdate();

        TMP_TextInfo textInfo = tmpText.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            Vector3[] verts = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;

            for (int j = 0; j < 4; j++)
            {
                Vector3 orig = verts[charInfo.vertexIndex + j];
                verts[charInfo.vertexIndex + j] = orig + new Vector3(Mathf.Sin(Time.time * wobbleSpeed + orig.x) * wobbleAmount,
                    Mathf.Sin(Time.time * wobbleSpeed + orig.y) * wobbleAmount, 0);
            }
        }

        // Update the mesh vertex values
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            tmpText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}