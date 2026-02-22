using UnityEngine;
using TMPro;
using Febucci.UI; // Ensure you have the Febucci Text Animator namespace

public class RandomMessageGenerator : MonoBehaviour
{
    // Reference to the TextMeshProUGUI component to display the message
    public TextMeshProUGUI txtMessage;

    // Arrays to hold the strings, now editable in the Unity Inspector with default values
    private string[] purposeArray = new string[]
    {
        "This free app helps children develop early literacy skills by pairing images with text.",
        "Pairing images and text helps kids follow narratives better.",
        "This app was created by volunteers. You can support us by visiting our bookstore.",
        "Parents' section offers tips to assist children in learning.",
        "Parents' section includes scientific papers for deeper insights."
    };

    private string[] uniquenessArray = new string[]
    {
        "Some paragraphs have multiple illustrations for better understanding.",
        "Our books pair pictures with every paragraph for engagement.",
        "Our books designed to encourage kids to connect ideas and concepts.",
        "Audio and visual cues create an immersive experience.",
        "The reading options allow to silence the voice and read the book yourself.",
        "Some books offer two narrators to choose from."
    };

    private string[] jokeArray = new string[]
    {
        "<color=#550055><wave>Explore different topics through fun adventures in reading.</wave></color>",
        "<color=red><wave>We believe in Santa Claus!</wave></color>",
        "<color=#006400><wave>Let kids learn at their own pace through fun stories.</wave></color>",
        "<color=blue><wave>Why did the book see a doctor? It had a spine problem!</wave></color>",
        "<color=blue><wave>Reading takes you anywhere... even where socks vanish!</wave></color>"
    };

    // Start is called before the first frame update
    void Start()
    {
        GenerateRandomMessage();
    }

    // Method to generate and display the random message
    void GenerateRandomMessage()
    {
        if (txtMessage == null)
        {
            Debug.LogError("TextMeshProUGUI component is not assigned. Please assign it in the Inspector.");
            return;
        }

        if (purposeArray == null || uniquenessArray == null || jokeArray == null ||
            purposeArray.Length == 0 || uniquenessArray.Length == 0 || jokeArray.Length == 0)
        {
            Debug.LogError("One or more arrays are null or empty. Ensure all arrays are initialized with elements.");
            return;
        }

        // Select random messages
        string randomPurpose = purposeArray[Random.Range(0, purposeArray.Length)];
        string randomUniqueness = uniquenessArray[Random.Range(0, uniquenessArray.Length)];
        string randomContent = jokeArray[Random.Range(0, jokeArray.Length)];

        // Combine the strings into paragraphs and add formatting
        txtMessage.text = "This app requires a decent <color=#0000FF><b>internet connection</b></color>." + "\n\n" + 
            randomPurpose + "\n\n" + 
            randomUniqueness + "\n\n" + 
                          randomContent + "\n\n" +
                          "<color=#EE8800>Support our volunteer efforts by checking out our bookstore!</color>";
    }
    /*
     This app requires a decent <color=#0000FF><b>internet connection</b></color>. 
       It may not perform well when the connection is unreliable. 
       
       - Designed for well-behaved children.
       - <wave>We believe in Santa Claus.</wave>
       
     */
    
}
