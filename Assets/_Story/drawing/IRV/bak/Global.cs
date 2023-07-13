using UnityEngine;

public class Global : MonoBehaviour
{
    public static int currentSpriteOrderIndex = 0; //used in Time Prepositions game

    public const string TYPE_STATIC = "static";
    public const string TYPE_DYNAMIC = "dynamic";
    public const string TYPE_BACKGROUND = "background";

    public static float ScXGlobal = 1.0f; 
    public static float ScYGlobal = 1.0f; 
    public static Color colorWhite = new Color(1f, 1f, 1f, 1f);
    public static Color colorGold = new Color(255f / 255f, 255f / 255f, 55f / 255f, 1f);
    public static Color colorMagenta = new Color(147f / 255f, 42f / 255f, 142f / 255f, 1f);
    public static Color colorGreenGame = new Color(159f / 255f, 204f / 255f, 137f / 255f, 1f);
    public static Color colorBlueGame = new Color(137f / 255f, 159f / 255f, 204f / 255f, 1f);
    public static Color colorRedGame = new Color(204f / 255f, 137f / 255f, 159f / 255f, 1f);
    public static Color colorGreenLegend = new Color(3f / 255f, 84f / 255f, 10f / 255f, 1f);
    public static Color colorRedLegend = new Color(1f, 0f, 0f, 1f);
    public static Color colorBlueLight = new Color(28f / 255f, 241f / 255f, 243f / 255f, 1f);
    public static Color colorBlue2Light = new Color(133f / 255f, 136f / 255f, 255f / 255f, 1f);
    public static Color colorYellow = new Color(234f / 255f, 235f / 255f, 104f / 255f, 1f);
    public static float targetZcoordinate = 0;
    
    public static bool IsOrderOfTargetsImportantInThisGame()
    {
        return false;
    }
}