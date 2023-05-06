using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Grogan.Effects2D
{
    public class DemoButton : MonoBehaviour
    {

        [SerializeField] string sceneName;
        [SerializeField] GameObject outline;

        public void OnClick()
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        void Update()
        {
            if (outline != null) outline.SetActive(EventSystem.current.currentSelectedGameObject == this.gameObject);
        }
    }
}
