using UnityEngine;
using UnityEngine.UI;

public class SpriteEffects : MonoBehaviour
{
    public Image originalImage; // Reference to the original Sprite
    float blurAmount = 2; // Adjust the blur amount as needed
    
    public void BlurSprite()
    {
        Sprite originalSprite = originalImage.sprite;
        Sprite blurredSprite = ApplyEffect(originalSprite, blurAmount, 0, 0);
        originalImage.sprite  = blurredSprite;
    }

    public static Sprite ApplyEffect(Sprite sprite, float blur, float saturation, float brightness)
    {
        // Get the sprite's texture
        Texture2D texture = sprite.texture;

        // Create a new readable Texture2D and copy the original texture's pixels
        Texture2D readableTexture = new Texture2D(texture.width, texture.height);
        RenderTexture tempRT = RenderTexture.GetTemporary(texture.width, texture.height);
        Graphics.Blit(texture, tempRT);
        RenderTexture previousRenderTexture = RenderTexture.active;
        RenderTexture.active = tempRT;
        readableTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        readableTexture.Apply();
        RenderTexture.active = previousRenderTexture;
        RenderTexture.ReleaseTemporary(tempRT);

        // Create a new Texture2D to store the processed image
        Texture2D processedTexture = new Texture2D(readableTexture.width, readableTexture.height);

        // Apply Box blur to the texture
        Texture2D blurredTexture = ApplyBoxBlur(readableTexture, blur);

        // Loop through the pixels of the blurred texture
        for (int x = 0; x < blurredTexture.width; x++)
        {
            for (int y = 0; y < blurredTexture.height; y++)
            {
                Color color = blurredTexture.GetPixel(x, y);

                // Apply saturation
                float grey = color.grayscale;
                color = Color.Lerp(new Color(grey, grey, grey), color, saturation);

                // Apply brightness
                color *= brightness;

                processedTexture.SetPixel(x, y, color);
            }
        }

        processedTexture.Apply();

        // Create a new Sprite with the processed texture
        Rect spriteRect = new Rect(0, 0, processedTexture.width, processedTexture.height);
        Vector2 spritePivot = new Vector2(0.5f, 0.5f);
        Sprite processedSprite = Sprite.Create(processedTexture, spriteRect, spritePivot);

        return processedSprite;
    }

    private static Texture2D ApplyBoxBlur(Texture2D texture, float blur)
    {
        Texture2D blurredTexture = new Texture2D(texture.width, texture.height);

        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
            {
                Color color = new Color(0, 0, 0, 0);
                int samples = 0;

                for (int offsetX = -Mathf.FloorToInt(blur); offsetX <= Mathf.CeilToInt(blur); offsetX++)
                {
                    for (int offsetY = -Mathf.FloorToInt(blur); offsetY <= Mathf.CeilToInt(blur); offsetY++)
                    {
                        int newX = x + offsetX;
                        int newY = y + offsetY;

                        if (newX >= 0 && newX < texture.width && newY >= 0 && newY < texture.height)
                        {
                            color += texture.GetPixel(newX, newY);
                            samples++;
                        }
                    }
                }

                color /= samples;
                blurredTexture.SetPixel(x, y, color);
            }
        }

        blurredTexture.Apply();
        return blurredTexture;
    }
}
