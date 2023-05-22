using System;
using UnityEngine;
using System.Collections.Generic;
using Miniscript;
using QFSW.QC;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class Settings
{
    public string Content;
}

public class Scriptlet
{
    public string Content;
}

public class PRCharacter
{
    public string name;
    public string image;
    public string text;
}

public class ButtonStruct
{
    public Button button;
    public string message;
}

public class PRScript : MonoBehaviour
{
    public string scriptURL;
    public string convGoodPeopleLocal = "http://localhost:8080/api/files/download/stories/GoodPeople/GoodPeople_chunks_script.txt";
    public string convPeterRabbitLocal = "http://localhost:8080/api/files/download/stories/The_Tale_of_Peter_Rabbit_(1901)_script_mp3.txt";
    public string convSeaStoryEnLocal = "http://localhost:8080/api/files/download/stories/Sea_Story_en/SeaStory_en_chunks_script.txt";
    public string convSeaStoryRuLocal = "http://localhost:8080/api/files/download/stories/Sea_Story_ru/SeaStory_ru.txt";
    public string convHumanSoundsLocal = "http://localhost:8080/api/files/download/Stories/HumansMakingSounds/HumansMakingSounds_chunks_script.txt";
    public string convTAHFS3 = "http://35.90.126.120:8080/api/files/download/Stories/TimmyAndHisFamily/TimmyAndHisFamily01.txt";
    public string convHumanSoundsS3 = "http://35.90.126.120:8080/api/files/download/Stories/HumansMakingSounds/HumansMakingSounds_chunks_script.txt";
    public StoryStepsUI storyStepsUI;
    public AudioPlayer audioPlayer;
    public PRVideoPlayer videoPlayer;
    public MicrosoftTextToSpeech microsoftTextToSpeech;
    public AudioAndTextPlayer audioAndTextPlayer;
    public ParentalGate parentalGate;
    public Button buttonParentalGate;
    public GameObject titlePanel;
    public TitlePage titlePage;
    public int nCurrentStep = 0;

    private Interpreter _interpreter;
    private List<Scriptlet> _scriptlets;
    private Settings _settings;
    private List<PRCharacter> _characters;
    private List<ButtonStruct> buttonStructs = new List<ButtonStruct>();

    public string baseURL = "";
    
    private void parse(string script)
    {
        List<string> lines = PRUtils.SplitStringIntoLines(script);

        _settings = new Settings();
        _scriptlets = new List<Scriptlet>();
        _characters = new List<PRCharacter>();

        bool firstStepFound = false;
        int index = 0;
        while (index < lines.Count)
        {
            if (lines[index].StartsWith("Step") || lines[index].StartsWith("////////[chunk"))
            {
                firstStepFound = true;
                //index++;
                Scriptlet scriptlet = new Scriptlet();
                scriptlet.Content = "";
                
                scriptlet.Content += lines[index] + "\n";
                index++;
                while (index < lines.Count && !lines[index].StartsWith("Step") && !lines[index].StartsWith("////////[chunk"))
                {
                    scriptlet.Content += lines[index] + "\n";
                    index++;
                }

                _scriptlets.Add(scriptlet);
            }
            else
            {
                if (!firstStepFound)
                {
                    _settings.Content += lines[index] + "\n";
                }

                index++;
            }
        }

        for (int i = 0; i < _scriptlets.Count; i++)
        {
            storyStepsUI.AddStoryStep(_scriptlets[i].Content, i);
        }

        //AlertDialogManager.Instance.ShowAlertDialog("executing: " + _settings.Content);
        ExecuteScriptlet(_settings.Content);
    }

    void Start()
    {
        storyStepsUI.prScript = this;
        
        if (Globals.g_scriptName != null)
            scriptURL = Globals.g_scriptName;
        
        baseURL = PRUtils.RemoveFileNameFromUrl(scriptURL);
        audioPlayer.baseURL = PRUtils.RemoveFileNameFromUrl(scriptURL);
        videoPlayer.baseURL = PRUtils.RemoveFileNameFromUrl(scriptURL);
        audioAndTextPlayer.baseURL = PRUtils.RemoveFileNameFromUrl(scriptURL);
        Reload();
    }
    void OnDestroy()
    {
        _interpreter?.Reset();
        Debug.Log("OnDestroy PRScript");
    }

    public void Reload()
    {
        storyStepsUI.Cleanup();
        StartCoroutine(PRUtils.DownloadFile(scriptURL, (content) => { parse(content); }));
    }

    void SetupInterpreter()
    {
        _interpreter = new Interpreter();
        Intrinsic f = Intrinsic.Create("DisplayTitle");
        f.AddParam("title", "");
        f.code = (context, partialResult) =>
        {
            string title = context.GetVar("title").ToString();
            storyStepsUI.DisplayTitle(title);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("Characters");
        f.AddParam("characters", "");
        f.code = (context, partialResult) =>
        {
            string characters = context.GetVar("characters").ToString();
            characters = characters.Trim('[', ']');
            string[] characterArray = characters.Split(new[] { ", " }, StringSplitOptions.None);
            storyStepsUI.HideAllCharactersExcept(characterArray);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddCharacter");
        f.AddParam("charname", "");
        f.AddParam("url", "");
        f.AddParam("text", "");
        f.code = (context, partialResult) =>
        {
            string charname = context.GetVar("charname").ToString();
            string url = context.GetVar("url").ToString();
            string text = context.GetVar("text").ToString();
            PRCharacter prCharacter = new PRCharacter();
            prCharacter.name = charname;
            prCharacter.text = text;
            prCharacter.image = NormalizeURL(url);  
            _characters.Add(prCharacter);
            storyStepsUI.AddCharacter(charname, NormalizeURL(url), text);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("CreateButton");
        f.AddParam("x", 0f);
        f.AddParam("y", 0f);
        f.AddParam("width", 0f);
        f.AddParam("imageURL", "");
        f.AddParam("message", "");
        f.code = (context, partialResult) =>
        {
            float x = context.GetVar("x").FloatValue();
            float y = context.GetVar("y").FloatValue();
            float width = context.GetVar("width").FloatValue();
            string imageURL = context.GetVar("imageURL").ToString();
            string msg = context.GetVar("message").ToString();
            Button btn = storyStepsUI.CreateButton( x, y, width, NormalizeURL(imageURL), msg);
            buttonStructs.Add(new ButtonStruct() { button = btn, message = msg });
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("CharacterSpeaks");
        f.AddParam("character", "");
        f.AddParam("speech", "");
        f.code = (context, partialResult) =>
        {
            string character = context.GetVar("character").ToString();
            string speech = context.GetVar("speech").ToString();
            Debug.Log("TODO: Script CharacterSpeaks: " + character + " " + speech);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("GoTo");
        f.AddParam("label", "");
        f.code = (context, partialResult) =>
        {
            string label = context.GetVar("label").ToString();
            if (label.ToLower() == "next")
                NextStep();
            else if (label.ToLower() == "prev")
                PrevStep();
            else
                GotoStep(label);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("Speak");
        f.AddParam("text", "");
        f.AddParam("voice", "");
        f.AddParam("rate", "");
        f.AddParam("pitch", "");
        f.code = (context, partialResult) =>
        {
            string text = context.GetVar("text").ToString();
            string voice = context.GetVar("voice").ToString();
            string rate = context.GetVar("rate").ToString();
            string pitch = context.GetVar("pitch").ToString();
            
            microsoftTextToSpeech.Speak(text, voice, rate, pitch);

            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("SelectCharacter");
        f.AddParam("character", "");
        f.code = (context, partialResult) =>
        {
            string character = context.GetVar("character").ToString();
            Debug.Log("TODO: Script SelectCharacter: " + character);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddAudio");
        f.AddParam("audioname", "");
        f.AddParam("url", "");
        f.code = (context, partialResult) =>
        {
            string audioname = context.GetVar("audioname").ToString();
            string url = context.GetVar("url").ToString();
            StartCoroutine(audioPlayer.LoadAudioClip(audioname, url));
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("SetShoppingLink");
        f.AddParam("url", "");
        f.code = (context, partialResult) =>
        {
            string url = context.GetVar("url").ToString();
            parentalGate.url = url;
            if (url != "")
                buttonParentalGate.gameObject.SetActive(true);
            else
                buttonParentalGate.gameObject.SetActive(false);
            
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddVideo");
        f.AddParam("videoname", "");
        f.AddParam("url", "");
        f.code = (context, partialResult) =>
        {
            string videoname = context.GetVar("videoname").ToString();
            string url = context.GetVar("url").ToString();
            videoPlayer.LoadVideo(url);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("DisplayMainImage");
        f.AddParam("url", "");
        f.code = (context, partialResult) =>
        {
            string url = context.GetVar("url").ToString();
            storyStepsUI.DisplayMainImage(url);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddGalleryImage");
        f.AddParam("url", "");
        f.code = (context, partialResult) =>
        {
            string url = context.GetVar("url").ToString();
            storyStepsUI.AddGalleryImage(url);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddGallerySound");
        f.AddParam("url", "");
        f.code = (context, partialResult) =>
        {
            string url = context.GetVar("url").ToString();
            storyStepsUI.AddGallerySound(url);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("DisplayBackgroundImage");
        f.AddParam("url", "");
        f.code = (context, partialResult) =>
        {
            string url = context.GetVar("url").ToString();
            storyStepsUI.DisplaybackgoundImage(url);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("DisplayTitlePage");
        f.AddParam("title", "");
        f.AddParam("author", "");
        f.AddParam("link", "");
        f.code = (context, partialResult) =>
        {
            string title = context.GetVar("title").ToString();
            string author = context.GetVar("author").ToString();
            string link = context.GetVar("link").ToString();
            titlePage.gameObject.SetActive(true);
            titlePage.SetTitlePage(title, author, link);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("HideTitlePage");
        f.code = (context, partialResult) =>
        {
            titlePanel.gameObject.SetActive(false);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("PlayAudio");
        f.AddParam("audioname", "");
        f.AddParam("begin", 0);
        f.AddParam("end", 0);
        f.code = (context, partialResult) =>
        {
            string audioname = context.GetVar("audioname").ToString();
            float fBegin = context.GetVar("begin").FloatValue();
            float fEnd = context.GetVar("end").FloatValue();
            audioPlayer.PlayAudio(audioname, fBegin, fEnd);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("PlayAudio");
        f.AddParam("audioname", "");
        f.AddParam("begin", 0);
        f.AddParam("end", 0);
        f.code = (context, partialResult) =>
        {
            string audioname = context.GetVar("audioname").ToString();
            float fBegin = context.GetVar("begin").FloatValue();
            float fEnd = context.GetVar("end").FloatValue();
            audioPlayer.PlayAudio(audioname, fBegin, fEnd);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("PlayAudioText");
        f.AddParam("audioname", "");
        f.AddParam("timings", "");
        f.AddParam("content", "");
        f.code = (context, partialResult) =>
        {
            string audioname = context.GetVar("audioname").ToString();
            string timings = context.GetVar("timings").ToString();
            string content = context.GetVar("content").ToString();
            audioAndTextPlayer.SetActive(true);
            audioAndTextPlayer.Play(audioname, timings); 
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("PlayAudioAndText");
        f.AddParam("chunkname", "");
        f.AddParam("content", "");
        f.code = (context, partialResult) =>
        {
            // chunk_1
            // chunk_1_0.mp3  chunk_1_-10.mp3 chunk_1_-20.mp3 chunk_1_-30.mp3
            // chunk_1_0.chunk_1_0_timings.json chunk_1_-10.mp3 chunk_1_-20_timings.json chunk_1_-30_timings.json
            string chunkname = context.GetVar("chunkname").ToString();
            string audioname = $"{chunkname}_{Globals.g_Rate}.mp3";  ;
            string timings = $"{chunkname}_{Globals.g_Rate}_timings.json"; 
            string content = context.GetVar("content").ToString();
            audioAndTextPlayer.SetActive(true);
            audioAndTextPlayer.Play(audioname, timings); 
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("SetAudioTextFont");
        f.AddParam("fontname", "");
        f.AddParam("fontsize", 20);
        f.code = (context, partialResult) =>
        {
            string fontname = context.GetVar("fontname").ToString();
            int fontsize = context.GetVar("fontsize").IntValue();
            audioAndTextPlayer.SetFont(fontname, fontsize); 
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("PlayVideo");
        f.AddParam("videoname", "");
        f.AddParam("begin", 0);
        f.AddParam("end", 0);
        f.code = (context, partialResult) =>
        {
            Debug.Log("PlayVideo");
            string videoname = context.GetVar("videoname").ToString();
            float fBegin = context.GetVar("begin").FloatValue();
            float fEnd = context.GetVar("end").FloatValue();
            videoPlayer.PlaySegment(fBegin, fEnd);
            return new Intrinsic.Result(ValNumber.one);
        };
        ConfigOutput();
    }

    [Command]
    void RunScript(string script)
    {
        SetupInterpreter();
        _interpreter.Reset(script);
        _interpreter.Compile();
        _interpreter.RunUntilDone(0.01);
    }

    public void ExecuteStep(int index)
    {
        if (index < 0 || index >= _scriptlets.Count)
            return;
        bool bStepChanged = SetCurrentStep(index);
        if (bStepChanged) 
            ExecuteScriptlet(_scriptlets[nCurrentStep].Content);
    }

    void ExecuteScriptlet(string scriptlet)
    {
        RunScript(scriptlet);
        // if (scriptlet == null)
        //     return;
        //
        // List<string> lines = PRUtils.SplitStringIntoLines(scriptlet);
        // foreach (string s in lines)
        // {
        //     RunScript(s);
        // }
    }

    void ConfigOutput()
    {
        // Define what to do with output from the interpreter.
        // We'll pass it to our output.PrintLine method, but wrap it in some color
        // tags depending on what sort of output it is.
        _interpreter.standardOutput = (s) => Debug.Log(s);
        _interpreter.implicitOutput = (s) => Debug.Log(
            "<color=#66bb66>" + s + "</color>");
        _interpreter.errorOutput = (s) =>
        {
            AlertDialogManager.Instance.ShowAlertDialog("error in script: " + s + 
                                                        "\n The script content:\n <color=#bb00bb>" + 
                                                        _interpreter.source +"</color>");
            Debug.LogWarning(s);
            Debug.Log("<color=red>" + s + "</color>");
            // ...and in case of error, we'll also stop the interpreter.
            _interpreter.Stop();
        };
    }
    
    PRCharacter FindCharacter(string name)
    {
        return _characters.Find(x => x.name == name);
    }
    
    public bool SetCurrentStep(int index)
    {
        if (index >= 0 && index < _scriptlets.Count)
        {
            nCurrentStep = index;
            Debug.Log($"Current step was set to " + index);
            return true;
        }
        Debug.Log($"Could not set step to " + index);
        return false;
    }

    public void PrevStep()
    {
        bool bStepChanged = SetCurrentStep(nCurrentStep - 1);
        if (!bStepChanged)
            return;
        
        storyStepsUI.SelectStep(nCurrentStep);
        ExecuteStep(nCurrentStep);
        SetUIAccordingToCurrentStep();
    }
    
    public void NextStep()
    {
        bool bStepChanged = SetCurrentStep(nCurrentStep + 1);
        if (!bStepChanged)
            return;
        storyStepsUI.SelectStep(nCurrentStep);
        ExecuteStep(nCurrentStep);
        SetUIAccordingToCurrentStep();
    }

    public void Home()
    {
        Globals.g_scriptName = "";
        SceneManager.LoadScene("_Library");
    }
    
    public void ReplayCurrenStep()
    {
        ExecuteStep(nCurrentStep);
        SetUIAccordingToCurrentStep();
    }

    private void GotoStep(string label)
    {
        SetUIAccordingToCurrentStep();
    }

    
    private void SetUIAccordingToCurrentStep()
    {
        if (nCurrentStep <= 0)
            SetActiveButtons("Prev", false);
        else
            SetActiveButtons("Prev", true);

        if (nCurrentStep >= _scriptlets.Count - 1)
            SetActiveButtons("Next", false);
        else
            SetActiveButtons("Next", true);
            

        
    }

    private void SetActiveButtons(string message, bool bActive)
    {
        foreach (var buttonStruct in buttonStructs)
        {
            if (buttonStruct.message.ToLower() == message.ToLower())
                buttonStruct.button.gameObject.SetActive(bActive);
        }
    }
    
    public void OnButtonClick(string customString)
    {
       if (customString.ToLower() == "Next".ToLower())
       {
           NextStep();
       }
       else if (customString.ToLower() == "Prev".ToLower())
       {
           PrevStep();
       }
       else if (customString.ToLower() == "Home".ToLower())
       {
           Home();
       }
    }

    string NormalizeURL(string url)
    {
        if (url.ToLower().StartsWith("http") == false)
        {
            url = baseURL + url;
        }
        return url;
    }


}