/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading the Code Monkey Utilities
    I hope you find them useful in your projects
    If you have any questions use the contact form
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System;
using UnityEngine;
using UnityEngine.UI;

namespace CodeMonkey.Utils {

    /*
     * Sprite in the UI
     * */
    public class UI_Sprite {

        private static Transform GetCanvasTransform() {
            return UtilsClass.GetCanvasTransform();
        }


        public GameObject gameObject;
        public Image image;
        public RectTransform rectTransform;


        public UI_Sprite(Transform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 size, Color color) {
            rectTransform = UtilsClass.DrawSprite(sprite, parent, anchoredPosition, size, "UI_Sprite");
            gameObject = rectTransform.gameObject;
            image = gameObject.GetComponent<Image>();
            image.color = color;
        }
        public void SetColor(Color color) {
            image.color = color;
        }
        public void SetSprite(Sprite sprite) {
            image.sprite = sprite;
        }
        public void SetSize(Vector2 size) {
            rectTransform.sizeDelta = size;
        }
        public void SetAnchoredPosition(Vector2 anchoredPosition) {
            rectTransform.anchoredPosition = anchoredPosition;
        }
        public Button_UI AddButton(Action ClickFunc, Action MouseOverOnceFunc, Action MouseOutOnceFunc) {
            Button_UI buttonUI = gameObject.AddComponent<Button_UI>();
            if (ClickFunc != null)
                buttonUI.ClickFunc = ClickFunc;
            if (MouseOverOnceFunc != null)
                buttonUI.MouseOverOnceFunc = MouseOverOnceFunc;
            if (MouseOutOnceFunc != null)
                buttonUI.MouseOutOnceFunc = MouseOutOnceFunc;
            return buttonUI;
        }
        public void DestroySelf() {
            UnityEngine.Object.Destroy(gameObject);
        }

    }
}