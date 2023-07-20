using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class VAPI : MonoBehaviour
{
    public List<string> galleryImages = new List<string>
        { "Bg1.jpg", "Bg2.jpg", "Bg3.jpg", "Bg4.jpg", "Bg6.jpg", "Bg7.jpg", "Bg8.jpg" };

    public int curGalleryImage = 0;
    public Slider slider;
    VVScene vVScene;

    public void Start()
    {
        vVScene = gameObject.GetComponent<VVScene>();
    }


    public IEnumerator CheckAllSpritesReadyCoroutine(float timeout, Action<bool> callback)
    {
        float timeElapsed = 0f;

        while (timeElapsed < timeout)
        {
            bool allReady = true;

            foreach (KeyValuePair<string, GameObject> kvp in vVScene._gameObjectMap)
            {
                VSprite vSpriteComponent = kvp.Value.GetComponent<VSprite>();
                if (vSpriteComponent != null && !vSpriteComponent.isReady)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                callback.Invoke(true);
                yield break;
            }

            yield return null;

            timeElapsed += Time.deltaTime;
        }

        callback.Invoke(false);
    }

    // This function will work correctly with patterns that have * at the start, end, or middle, such as won*, *derful, or won*ful.
    // It does not support multiple *s or *s at arbitrary positions.
    public static bool IsMatch(string pattern, string value)
    {
        if (pattern.EndsWith("*"))
        {
            // If pattern ends with *, use StartsWith
            return value.StartsWith(pattern.TrimEnd('*'));
        }
        else if (pattern.StartsWith("*"))
        {
            // If pattern starts with *, use EndsWith
            return value.EndsWith(pattern.TrimStart('*'));
        }
        else
        {
            // If pattern contains * in the middle, use Contains
            return value.Contains(pattern.Replace("*", ""));
        }
    }


    public void MoveCurveBehaviourObjects(GameObject goParent, float proportionOfTheWay)
    {
        CurveBehaviour[] curveBehaviours;
        if (goParent == null)
            curveBehaviours = vVScene.gameObject.GetComponentsInChildren<CurveBehaviour>();
        else
            curveBehaviours = goParent.GetComponentsInChildren<CurveBehaviour>();
        foreach (CurveBehaviour curveBehaviour in curveBehaviours)
        {
            curveBehaviour.MoveTo(proportionOfTheWay);
        }
    }

    public void MoveCurveBehaviourObjectsForward(string vspriteId)
    {
        StartCoroutine(MoveCurveBehaviourObjectsCoroutine(vspriteId, true));
    }

    public void MoveCurveBehaviourObjectsBackward(string vspriteId)
    {
        StartCoroutine(MoveCurveBehaviourObjectsCoroutine(vspriteId, false));
    }

    public IEnumerator MoveCurveBehaviourObjectsCoroutine(string vspriteShortId, bool bForward)
    {
        GameObject goParent = vVScene.FindInGoMap(vspriteShortId);
        CurveBehaviour[] curveBehaviours;
        if (goParent == null)
            curveBehaviours = GameObject.FindObjectsOfType<CurveBehaviour>();
        else
            curveBehaviours = goParent.GetComponentsInChildren<CurveBehaviour>();
        // Modify the name to work as a regex pattern
        foreach (CurveBehaviour curveBehaviour in curveBehaviours)
        {
            if (bForward)
                curveBehaviour.MoveForward();
            else
                curveBehaviour.MoveBackward();
        }

        yield return null;
    }

    public static List<GameObject> GetAllChildGameObjects(GameObject goParent)
    {
        List<GameObject> gameObjects = new List<GameObject>();
        if (goParent != null)
        {
            AddChildrenRecursive(goParent.transform, gameObjects);
        }

        return gameObjects;
    }

    private static void AddChildrenRecursive(Transform parent, List<GameObject> gameObjects)
    {
        foreach (Transform child in parent)
        {
            gameObjects.Add(child.gameObject);
            AddChildrenRecursive(child, gameObjects);
        }
    }

    public void AddCurveBehaviour(string parentId, string nnname)
    {
        GameObject goParent = vVScene.FindInGoMap(parentId); 
        List<GameObject> gameObjects = GetAllChildGameObjects(goParent);

        foreach (var go in gameObjects)
        {
            if (IsMatch(nnname, go.name))
            {
                if (go.GetComponent<CurveBehaviour>() == null)
                {
                    CurveBehaviour curveBehaviour = go.AddComponent<CurveBehaviour>();
                    curveBehaviour.speed = 10f + UnityEngine.Random.Range(-1f, 1f);
                    var position = go.transform.position;
                    Vector3 waypoint1 = new Vector3(position.x, position.y, position.z);
                    Vector3 waypoint3 = new Vector3(position.x, -10, position.x + UnityEngine.Random.Range(-1f, 1f));
                    curveBehaviour.WavyMovement(waypoint1, waypoint3, 10 + (int)UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(0, 2));
                }
            }
        }
    }

    public void JumpCurveBehaviourToStart(GameObject goParent, string nnname)
    {
        CurveBehaviour[] curveBehaviours = goParent.GetComponentsInChildren<CurveBehaviour>();
        foreach (var cb in curveBehaviours)
        {
            cb.JumpToStart();
        }
    }

    public void JumpCurveBehaviourToEnd(string parentId, string nnname)
    {
        GameObject goParent = vVScene.FindInGoMap(parentId);
        CurveBehaviour[] curveBehaviours = goParent.GetComponentsInChildren<CurveBehaviour>();
        foreach (var cb in curveBehaviours)
        {
            cb.JumpToEnd();
        }
    }

    public IEnumerator LoadGallerySlideCoroutine(string vspriteShortId, string parentId, string imageUrl)
    {
        // Ensure all sprites in the current slide are loaded.
        yield return StartCoroutine(CheckAllSpritesReadyCoroutine(10, result => { }));

        if (vVScene.FindInGoMap(vspriteShortId) != null)
        {
            yield return StartCoroutine(MoveCurveBehaviourObjectsCoroutine(vspriteShortId, false));
            vVScene.RemoveFromGoMapAndDestroy(vspriteShortId);
        }

        StartCoroutine(vVScene.AddVSpriteCoroutine(vspriteShortId, parentId, imageUrl, vVScene.XFromGrid(0),
            vVScene.YFromGrid(0), vVScene.XFromGrid(1000), vVScene.YFromGrid(1000), false));

        yield return StartCoroutine(CheckAllSpritesReadyCoroutine(10, result =>
        {
            if (result)
            {
                vVScene.ShowGObjects(vspriteShortId, 0);
                vVScene.CreateVSpriteGrid(vspriteShortId, 100, 10);
                AddCurveBehaviour(vspriteShortId, vspriteShortId + "_*");
                JumpCurveBehaviourToEnd(vspriteShortId, vspriteShortId + "_*");
                StartCoroutine(MoveCurveBehaviourObjectsCoroutine(vspriteShortId, false));
            }
        }));

        // Gradually show the new slide.
        //yield return StartCoroutine(MoveCurveBehaviourObjectsCoroutine(vspriteId, false));
    }

    #region GUI

    public void NextGallerySlide()
    {
        curGalleryImage++;
        if (curGalleryImage >= galleryImages.Count) curGalleryImage = 0;
        StartCoroutine(LoadGallerySlideCoroutine("gallery_slide", "", galleryImages[curGalleryImage]));
    }

    public void MoveCurveBehaviourObjectsToSliderPos()
    {
        float proportionOfTheWay = 0;
        if (slider != null) proportionOfTheWay = slider.value;
        MoveCurveBehaviourObjects(vVScene.gameObject, proportionOfTheWay);
    }

    public void OnButtonForward()
    {
        MoveCurveBehaviourObjectsForward("background");
    }

    public void OnButtonBackward()
    {
        MoveCurveBehaviourObjectsBackward("background");
    }

    #endregion
}