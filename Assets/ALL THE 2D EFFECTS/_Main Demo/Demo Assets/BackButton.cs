using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grogan.Effects2D
{
    public class BackButton : MonoBehaviour
    {
        public void OnClick()
        {
            SceneManager.LoadScene("Demo Browser");
        }
    }
}