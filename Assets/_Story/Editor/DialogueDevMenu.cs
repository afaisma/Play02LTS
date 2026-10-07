using UnityEditor;
using UnityEngine;

// Developer shortcut: open the dialogue test book (not in the catalog) straight from the master
// folder on this Mac. Works in Play mode, from any screen after the catalog has loaded.
public static class DialogueDevMenu
{
    private const string TestBook =
        "file:///Users/alexanderfaisman/dev/FileServer/uploads/stories/JackAndTheBeanstalk_dialogues/" +
        "jack_and_the_beanstalk_dialogues_chunks_script.txt";

    [MenuItem("Tools/ReadingBuddy/Open dialogue test book (Jack)", false, 300)]
    private static void OpenTestBook()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Dialogue test book", "Press Play first, wait for the Home screen, then choose this menu item again.", "OK");
            return;
        }
        SweepActions.Perform("bookfile", TestBook);
    }
}
