using System.Collections;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapManager : MonoBehaviour
{
    public void StopAndDisableParticles()
    {
        VPlayParticle[] vPlayParticles = FindObjectsOfType<VPlayParticle>();
        foreach (VPlayParticle vpp in vPlayParticles)
        {
            vpp.gameObject.SetActive(false);
        }

        ParticleImage[] particleImages = FindObjectsOfType<ParticleImage>();
        foreach (ParticleImage pi in particleImages)
        {
            pi.Stop();
            pi.gameObject.SetActive(false);
        }

        ParticleSystem[] particles = FindObjectsOfType<ParticleSystem>();
        foreach (ParticleSystem ps in particles)
        {
            ps.Stop();
            ps.gameObject.SetActive(false);
        }
    }
    
    public void HandleButtonClick(string buttonName)
    {
        // Debug.Log($"Button '{buttonName}' was clicked.");
        StopAndDisableParticles();
        Globals.GotoLibrary(buttonName);
        /*
        Globals.g_libraryFilter = buttonName;
        SceneManager.LoadScene("_Library");
        */
    }
    
    // Public wrapper methods preserved so scene-side button onClick
    // wirings keep working (this component is referenced by _Settings,
    // _Parents, _Message scenes for their nav buttons). Bodies
    // delegated to Navigation (see Story/Navigation.cs).
    public void Settings() => Navigation.GoToSettings();
    public void Library()  => Navigation.GoToLibrary();

    public void GotoScene(string sceneName) => Navigation.GoToScene(sceneName);

    public static void GotoBook(string name)
    {
        Globals.GotoBook(name);
    }

}
