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
        "This app helps children develop early literacy skills by pairing images with text.",
        "Pairing images and text helps children to understand and follow a narrative.",
        "The Parent's and Caregiver's section provides tips for parents to help their children learn.",
        "The Parent's and Caregiver's section contains a list of scientific papers for deeper insights."
    };

    private string[] uniquenessArray = new string[]
    {
        "Some paragraphs have more than one illustration to enhance comprehension.",
        "Our unique books have pictures displayed with every paragraph for better engagement.",
        "Our books are designed to encourage children to make connections between ideas and concepts.",
        "The app integrates audio and visual cues for a fully immersive experience.",
        "The reading options allow you to silence the voice and read the book yourself.",
        "Some books have two narrators to choose from."
    };

    private string[] contentArray = new string[]
    {
        "<color=#550055><wave>Explore different topics through fun adventures in reading.</wave></color>",
        "<color=red><wave>We believe in Santa Claus... and that reading makes anything possible!</wave></color>",
        "<color=#006400><wave>Let kids discover stories at their own pace and learn in a way that's fun for them.</wave></color>",
        "<color=blue><wave>Why did the book go to the doctor? Because it had a spine problem!</wave></color>",
        "<color=blue><wave>Reading can take you anywhere... even to places where socks disappear!</wave></color>"
    };

    // Start is called before the first frame update
    void Start()
    {
        GenerateRandomMessage();
    }

    // Method to generate and display the random message
    void GenerateRandomMessage()
    {
        // Check if the arrays have elements before trying to access them
        if (purposeArray.Length == 0 || uniquenessArray.Length == 0 || contentArray.Length == 0)
        {
            Debug.LogError("One or more of the arrays are empty. Please ensure all arrays have elements.");
            return;
        }

        // Select random elements from each array
        string randomPurpose = purposeArray[Random.Range(0, purposeArray.Length)];
        string randomUniqueness = uniquenessArray[Random.Range(0, uniquenessArray.Length)];
        string randomContent = contentArray[Random.Range(0, contentArray.Length)];

        // Combine the strings into paragraphs and add formatting
        txtMessage.text = "This app requires a decent <color=#0000FF><b>internet connection</b></color>." + "\n\n" + 
            randomPurpose + "\n\n" + 
            randomUniqueness + "\n\n" + 
            randomContent;
    }
    /*
     This app requires a decent <color=#0000FF><b>internet connection</b></color>. 
       It may not perform well when the connection is unreliable. 
       
       - Designed for well-behaved children.
       - <wave>We believe in Santa Claus.</wave>
       
     */
    
}
