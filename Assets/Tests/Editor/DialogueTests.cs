using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for the dialogue rules (Assets/_Story/Dialogue): the command collector, the
// answer / silence rules, spoken-answer matching and the "any sound" trigger. No scene needed.
namespace ReadingBuddy.Tests
{
    public class DialogueTests
    {
        // ---- helpers ----

        private static DialogueScript NewScript(List<string> warnings = null)
        {
            var s = new DialogueScript();
            if (warnings != null) s.Warn = warnings.Add;
            s.BeginBook();
            return s;
        }

        // "What is Jack climbing?" beanstalk / house (/ more), answer beanstalk.
        private static DialogueSpec Jack(params string[] extraChoices)
        {
            var s = NewScript();
            s.BeginPage();
            s.Question("What is Jack climbing?", "gen//q9");
            s.Choice("beanstalk", "Beanstalk", "images//beanstalk.jpg", "beanstalk, bean stalk", -1, -1, -1);
            s.Choice("house", "House", "images//house.jpg", "", -1, -1, -1);
            foreach (string id in extraChoices) s.Choice(id, id, "", "", -1, -1, -1);
            s.Answer("beanstalk");
            return s.Take();
        }

        private static DialogueChoice Choice(string id, string text, string words = "")
        {
            var c = new DialogueChoice { id = id, text = text };
            foreach (string w in words.Split(','))
                if (w.Trim().Length > 0) c.words.Add(w.Trim());
            return c;
        }

        // ---- DialogueScript: commands -> one dialogue per page ----

        [Test]
        public void Script_CollectsQuestionChoicesAnswerPraise()
        {
            var s = NewScript();
            s.BeginPage();
            s.Question(" What is Jack climbing? ", "gen//q9");
            s.Choice("beanstalk", "Beanstalk", "images//beanstalk.jpg", "beanstalk, bean stalk", -1, -1, -1);
            s.Choice("house", "House", "", "", 50, 30, 40);
            s.Answer("beanstalk");
            s.Praise("Yes! A beanstalk.", "gen//q9_praise");
            DialogueSpec d = s.Take();

            Assert.AreEqual("What is Jack climbing?", d.question);
            Assert.AreEqual("gen//q9", d.questionAudio);
            Assert.AreEqual(2, d.choices.Count);
            Assert.IsTrue(d.choices[0].HasPicture);
            CollectionAssert.AreEqual(new[] { "beanstalk", "bean stalk" }, d.choices[0].words);
            Assert.IsFalse(d.choices[1].HasPicture);
            Assert.AreEqual(40f, d.choices[1].width);
            Assert.AreEqual("beanstalk", d.answer);
            Assert.AreEqual("Yes! A beanstalk.", d.praiseText);
            Assert.AreEqual("gen//q9_praise", d.praiseAudio);
        }

        [Test]
        public void Script_IconChoices()
        {
            var c = new DialogueChoice { image = "icon:Check" };
            Assert.IsTrue(c.IsIcon);
            Assert.IsFalse(c.HasPicture);
            Assert.AreEqual("check", c.IconName);
        }

        [Test]
        public void Script_Defaults()
        {
            DialogueSpec d = Jack();
            Assert.AreEqual(DialogueInput.Both, d.settings.input);
            Assert.AreEqual(DialogueOnOther.Retry, d.settings.onOther);
            Assert.AreEqual(DialogueOnSilence.Repeat, d.settings.onSilence);
            Assert.AreEqual(8f, d.settings.silenceSeconds);
            Assert.IsTrue(d.settings.skip);
        }

        [Test]
        public void Script_SettingsBeforeTheFirstPage_ApplyToEveryPage()
        {
            var s = NewScript();
            s.Input("touch");
            s.OnSilence(12, "hint");
            s.Skip(false);

            s.BeginPage();
            s.Question("One?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            DialogueSpec first = s.Take();
            s.BeginPage();
            s.Question("Two?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            DialogueSpec second = s.Take();

            foreach (DialogueSpec d in new[] { first, second })
            {
                Assert.AreEqual(DialogueInput.Touch, d.settings.input);
                Assert.AreEqual(DialogueOnSilence.Hint, d.settings.onSilence);
                Assert.AreEqual(12f, d.settings.silenceSeconds);
                Assert.IsFalse(d.settings.skip);
            }
        }

        [Test]
        public void Script_SettingsInsideAPage_ApplyToThatPageOnly()
        {
            var s = NewScript();
            s.BeginPage();
            s.Question("One?", "");
            s.Input("sound");
            s.OnOther("accept");
            DialogueSpec first = s.Take();
            s.BeginPage();
            s.Question("Two?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            DialogueSpec second = s.Take();

            Assert.AreEqual(DialogueInput.Sound, first.settings.input);
            Assert.AreEqual(DialogueOnOther.Accept, first.settings.onOther);
            Assert.AreEqual(DialogueInput.Both, second.settings.input);
            Assert.AreEqual(DialogueOnOther.Retry, second.settings.onOther);
        }

        [Test]
        public void Script_BeginBook_ForgetsThePreviousBooksSettings()
        {
            var s = NewScript();
            s.Input("touch");
            s.BeginBook();
            s.BeginPage();
            s.Question("One?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            Assert.AreEqual(DialogueInput.Both, s.Take().settings.input);
        }

        [Test]
        public void Script_SettingValues_IgnoreLetterCase()
        {
            var s = NewScript();
            s.Input("SOUND");
            s.OnOther("Close");
            s.BeginPage();
            s.Question("One?", "");
            DialogueSpec d = s.Take();
            Assert.AreEqual(DialogueInput.Sound, d.settings.input);
            Assert.AreEqual(DialogueOnOther.Close, d.settings.onOther);
        }

        [Test]
        public void Script_UnknownSettingValue_WarnsAndKeepsTheOldValue()
        {
            var warnings = new List<string>();
            var s = NewScript(warnings);
            s.Input("shout");
            s.Input("3");
            s.OnOther("");
            s.OnSilence(5, "wait");
            s.BeginPage();
            s.Question("One?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            DialogueSpec d = s.Take();

            Assert.AreEqual(4, warnings.Count);
            Assert.AreEqual(DialogueInput.Both, d.settings.input);
            Assert.AreEqual(DialogueOnOther.Retry, d.settings.onOther);
            Assert.AreEqual(8f, d.settings.silenceSeconds); // a refused command changes nothing
        }

        [Test]
        public void Script_OnSilence_ZeroSecondsKeepsTheTime()
        {
            var s = NewScript();
            s.OnSilence(0, "close");
            s.BeginPage();
            s.Question("One?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            DialogueSpec d = s.Take();
            Assert.AreEqual(DialogueOnSilence.Close, d.settings.onSilence);
            Assert.AreEqual(8f, d.settings.silenceSeconds);
        }

        [Test]
        public void Script_IncompleteDialogue_IsNotShown()
        {
            var warnings = new List<string>();

            var s = NewScript(warnings);
            s.BeginPage();
            s.Choice("a", "A", "", "", -1, -1, -1);
            Assert.IsNull(s.Take(), "no question");

            s.BeginPage();
            s.Question("One?", "");
            Assert.IsNull(s.Take(), "no choices");

            s.BeginPage();
            s.Question("One?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            s.Answer("b");
            Assert.IsNull(s.Take(), "the answer is not a choice");

            Assert.AreEqual(3, warnings.Count);
        }

        [Test]
        public void Script_SoundDialogue_NeedsNoChoices()
        {
            var s = NewScript();
            s.BeginPage();
            s.Question("Say “splash”!", "gen//q5");
            s.Input("sound");
            Assert.IsNotNull(s.Take());
        }

        [Test]
        public void Script_PageCommandsBeforeAPage_WarnAndDoNothing()
        {
            var warnings = new List<string>();
            var s = NewScript(warnings);
            s.Question("One?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            s.Answer("a");
            s.Praise("Yes!", "");
            Assert.IsNull(s.Take());
            Assert.AreEqual(5, warnings.Count);
        }

        [Test]
        public void Script_DuplicateOrNamelessChoice_IsRefused()
        {
            var warnings = new List<string>();
            var s = NewScript(warnings);
            s.BeginPage();
            s.Question("One?", "");
            s.Choice("a", "A", "", "", -1, -1, -1);
            s.Choice("a", "Again", "", "", -1, -1, -1);
            s.Choice("  ", "No name", "", "", -1, -1, -1);
            Assert.AreEqual(1, s.Take().choices.Count);
            Assert.AreEqual(2, warnings.Count);
        }

        // ---- DialogueFlow: answers ----

        [Test]
        public void Flow_RightAnswer_Praises()
        {
            var flow = new DialogueFlow(Jack());
            Assert.AreEqual(DialogueFlow.Reaction.Correct, flow.Answer("beanstalk"));
            Assert.AreEqual(DialogueFlow.Step.Praise, flow.step);
            Assert.AreEqual("beanstalk", flow.chosen);
            Assert.AreEqual(1, flow.attempts);
        }

        [Test]
        public void Flow_OtherAnswer_FadesItAndAsksAgain()
        {
            var flow = new DialogueFlow(Jack("tree", "ladder"));
            Assert.AreEqual(DialogueFlow.Reaction.Retry, flow.Answer("house"));
            Assert.AreEqual(DialogueFlow.Step.Ask, flow.step);
            Assert.IsTrue(flow.faded.Contains("house"));
            Assert.IsFalse(flow.hint, "three choices are still open after one miss");
            Assert.AreEqual(3, flow.Open().Count);
        }

        [Test]
        public void Flow_SecondMiss_OutlinesTheAnswer()
        {
            var flow = new DialogueFlow(Jack("tree", "ladder"));
            flow.Answer("house");
            Assert.AreEqual(DialogueFlow.Reaction.Retry, flow.Answer("tree"));
            Assert.IsTrue(flow.hint);
            Assert.AreEqual(DialogueFlow.Reaction.Correct, flow.Answer("beanstalk"));
            Assert.AreEqual(3, flow.attempts);
        }

        [Test]
        public void Flow_TwoChoices_OneMissLeavesOnlyTheAnswer_SoItIsOutlined()
        {
            var flow = new DialogueFlow(Jack());
            flow.Answer("house");
            Assert.IsTrue(flow.hint);
            Assert.AreEqual(1, flow.Open().Count);
        }

        [Test]
        public void Flow_FadedOrUnknownChoice_IsIgnored()
        {
            var flow = new DialogueFlow(Jack());
            flow.Answer("house");
            Assert.AreEqual(DialogueFlow.Reaction.None, flow.Answer("house"));
            Assert.AreEqual(DialogueFlow.Reaction.None, flow.Answer("castle"));
            Assert.AreEqual(1, flow.attempts);
        }

        [Test]
        public void Flow_AfterThePraise_NothingMoreIsAccepted()
        {
            var flow = new DialogueFlow(Jack());
            flow.Answer("beanstalk");
            Assert.AreEqual(DialogueFlow.Reaction.None, flow.Answer("house"));
            Assert.AreEqual(DialogueFlow.SilenceReaction.None, flow.Silence());
        }

        [Test]
        public void Flow_OnOtherAccept_EveryChoiceCounts()
        {
            DialogueSpec d = Jack();
            d.settings.onOther = DialogueOnOther.Accept;
            var flow = new DialogueFlow(d);
            Assert.AreEqual(DialogueFlow.Reaction.Correct, flow.Answer("house"));
            Assert.AreEqual("house", flow.chosen);
        }

        [Test]
        public void Flow_NoAnswerDefined_EveryChoiceCounts()
        {
            DialogueSpec d = Jack();
            d.answer = "";
            Assert.AreEqual(DialogueFlow.Reaction.Correct, new DialogueFlow(d).Answer("house"));
        }

        [Test]
        public void Flow_OnOtherClose_ClosesOnAnotherAnswer()
        {
            DialogueSpec d = Jack();
            d.settings.onOther = DialogueOnOther.Close;
            var flow = new DialogueFlow(d);
            Assert.AreEqual(DialogueFlow.Reaction.Close, flow.Answer("house"));
            Assert.AreEqual(DialogueFlow.Step.Closed, flow.step);
        }

        [Test]
        public void Flow_SoundDialogue_AnySoundIsTheAnswer()
        {
            var s = NewScript();
            s.BeginPage();
            s.Question("Say “splash”!", "");
            s.Input("sound");
            var flow = new DialogueFlow(s.Take());
            Assert.AreEqual(DialogueFlow.Reaction.Correct, flow.Answer(""));
            Assert.AreEqual("", flow.chosen);
        }

        [Test]
        public void Flow_Skip_Closes()
        {
            var flow = new DialogueFlow(Jack());
            flow.Skip();
            Assert.AreEqual(DialogueFlow.Step.Closed, flow.step);
            Assert.AreEqual(DialogueFlow.Reaction.None, flow.Answer("beanstalk"));
        }

        // ---- DialogueFlow: silence ----

        [Test]
        public void Flow_Silence_RepeatsAFewTimesThenWaitsQuietly()
        {
            var flow = new DialogueFlow(Jack());
            for (int i = 0; i < DialogueFlow.MaxSilenceRepeats; i++)
                Assert.AreEqual(DialogueFlow.SilenceReaction.Repeat, flow.Silence());
            Assert.AreEqual(DialogueFlow.SilenceReaction.None, flow.Silence());
            Assert.AreEqual(DialogueFlow.Step.Ask, flow.step, "the dialogue stays open");
            Assert.IsFalse(flow.hint);
        }

        [Test]
        public void Flow_SilenceHint_OutlinesTheAnswer()
        {
            DialogueSpec d = Jack();
            d.settings.onSilence = DialogueOnSilence.Hint;
            var flow = new DialogueFlow(d);
            Assert.AreEqual(DialogueFlow.SilenceReaction.Repeat, flow.Silence());
            Assert.IsTrue(flow.hint);
        }

        [Test]
        public void Flow_SilenceClose_Closes()
        {
            DialogueSpec d = Jack();
            d.settings.onSilence = DialogueOnSilence.Close;
            var flow = new DialogueFlow(d);
            Assert.AreEqual(DialogueFlow.SilenceReaction.Close, flow.Silence());
            Assert.AreEqual(DialogueFlow.Step.Closed, flow.step);
        }

        // ---- DialogueSpeech ----

        [Test]
        public void Speech_Normalize()
        {
            Assert.AreEqual("a beanstalk", DialogueSpeech.Normalize("  A  Beanstalk! "));
            Assert.AreEqual("it's ok", DialogueSpeech.Normalize("It’s OK."));
            Assert.AreEqual("", DialogueSpeech.Normalize("?!"));
            Assert.AreEqual("", DialogueSpeech.Normalize(null));
        }

        [Test]
        public void Speech_Phrases_FromTheText_WithAndWithoutTheArticle()
        {
            CollectionAssert.AreEqual(new[] { "a beanstalk", "beanstalk" }, DialogueSpeech.Phrases(Choice("b", "A beanstalk")));
            CollectionAssert.AreEqual(new[] { "the giant", "giant" }, DialogueSpeech.Phrases(Choice("g", "The giant")));
            CollectionAssert.AreEqual(new[] { "big" }, DialogueSpeech.Phrases(Choice("big", "Big")));
            CollectionAssert.AreEqual(new[] { "another" }, DialogueSpeech.Phrases(Choice("x", "Another")), "\"an\" inside a word is not an article");
        }

        [Test]
        public void Speech_Phrases_ExtraWordsReplaceTheText()
        {
            CollectionAssert.AreEqual(new[] { "yes", "yeah", "yep" }, DialogueSpeech.Phrases(Choice("yes", "Yes", "yes, yeah, yep")));
        }

        [Test]
        public void Speech_Vocabulary_HasEveryPhraseOnce()
        {
            var choices = new[] { Choice("b", "Beanstalk", "beanstalk, bean stalk"), Choice("h", "House"), Choice("h2", "house") };
            CollectionAssert.AreEqual(new[] { "beanstalk", "bean stalk", "house" }, DialogueSpeech.Vocabulary(choices));
        }

        [Test]
        public void Speech_Match_FindsTheChoice()
        {
            var choices = new[] { Choice("beanstalk", "Beanstalk", "beanstalk, bean stalk"), Choice("house", "House") };
            Assert.AreEqual("beanstalk", DialogueSpeech.Match("beanstalk", choices));
            Assert.AreEqual("beanstalk", DialogueSpeech.Match("it is a bean stalk", choices));
            Assert.AreEqual("house", DialogueSpeech.Match("A HOUSE!", choices));
        }

        [Test]
        public void Speech_Match_WholeWordsOnly()
        {
            var choices = new[] { Choice("yes", "Yes"), Choice("no", "No") };
            Assert.IsNull(DialogueSpeech.Match("i know", choices));
            Assert.IsNull(DialogueSpeech.Match("yesterday", choices));
            Assert.IsNull(DialogueSpeech.Match("[unk]", choices));
            Assert.IsNull(DialogueSpeech.Match("", choices));
            Assert.IsNull(DialogueSpeech.Match(null, choices));
        }

        [Test]
        public void Speech_Match_TheChoiceSaidLastWins()
        {
            var choices = new[] { Choice("yes", "Yes"), Choice("no", "No") };
            Assert.AreEqual("yes", DialogueSpeech.Match("no yes", choices));
            Assert.AreEqual("no", DialogueSpeech.Match("yes no", choices));
        }

        // ---- Rewards ----

        [Test]
        public void Reward_DefaultIsAStar_BookAndPageCanChangeIt()
        {
            var s = new DialogueScript();
            s.BeginBook();
            s.Reward("none", "");
            s.BeginPage();
            s.Question("Q1?", ""); s.Choice("a", "A", "", "", -1, -1, -1);
            Assert.IsFalse(s.Take().settings.reward, "the book's default");
            s.BeginPage();
            s.Question("Q2?", ""); s.Choice("a", "A", "", "", -1, -1, -1);
            s.Reward("star", " gen//moo ");
            DialogueSpec d = s.Take();
            Assert.IsTrue(d.settings.reward);
            Assert.AreEqual("gen//moo", d.settings.rewardSound);
            s.BeginPage();
            s.Question("Q3?", ""); s.Choice("a", "A", "", "", -1, -1, -1);
            Assert.IsFalse(s.Take().settings.reward, "a page's setting does not leak into the next page");

            Assert.IsTrue(new DialogueSettings().reward, "without any command a question earns a star");
        }

        [Test]
        public void Reward_UnknownValue_WarnsAndChangesNothing()
        {
            var s = new DialogueScript();
            var warnings = new List<string>();
            s.Warn = warnings.Add;
            s.BeginBook();
            s.Reward("fireworks", "");
            s.BeginPage();
            s.Question("Q?", ""); s.Choice("a", "A", "", "", -1, -1, -1);
            Assert.IsTrue(s.Take().settings.reward);
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void PageEarnsStar_ReadFromTheScriptText()
        {
            const string page = "////////[chunk_1]\nDialogueQuestion \"Is it big?\", \"\"\nDialogueShow\n";
            Assert.IsTrue(DialogueScript.PageEarnsStar("", page));
            Assert.IsTrue(DialogueScript.PageEarnsStar(null, page));
            Assert.IsFalse(DialogueScript.PageEarnsStar("", "////////[chunk_1]\nPlayAudioAndText \"a\", \"b\"\n"), "no question");
            Assert.IsFalse(DialogueScript.PageEarnsStar("", "////////[chunk_1]\n// DialogueQuestion \"x\", \"\"\n"), "commented out");
            Assert.IsFalse(DialogueScript.PageEarnsStar("DialogueReward \"none\"\n", page), "the book's default");
            Assert.IsTrue(DialogueScript.PageEarnsStar("DialogueReward \"none\"\n", page + "DialogueReward \"star\", \"gen//moo\"\n"));
            Assert.IsFalse(DialogueScript.PageEarnsStar("", page + "DialogueReward(\"none\")\n"));
            Assert.IsFalse(DialogueScript.PageEarnsStar("DialogueReward \"None\"\n", page + "DialogueReward \"fireworks\"\n"),
                "an unknown value is ignored, as in the running book");
            Assert.IsFalse(DialogueScript.PageEarnsStar("", page.Replace("\n", "\r\n") + "DialogueReward \"none\"\r\n"), "Windows line ends");
        }

        [Test]
        public void StarRow_EveryAnswerEarns_SkippedStaysEmpty()
        {
            var row = new StarRow();
            row.Reset(new[] { 9, 2, 4 });
            Assert.AreEqual(3, row.Total);
            Assert.AreEqual(1, row.Earn(4), "the place in page order");
            Assert.AreEqual(1, row.Earn(4), "answering the same page again changes nothing");
            Assert.AreEqual(2, row.Earn(9));
            Assert.AreEqual(2, row.Earned);
            CollectionAssert.AreEqual(new[] { false, true, true }, row.States(), "page 2 was skipped");
        }

        [Test]
        public void StarRow_AQuestionTheTextDidNotShow_GetsAPlace()
        {
            var row = new StarRow();
            row.Reset(new[] { 2, 9 });
            Assert.AreEqual(1, row.Earn(5));
            CollectionAssert.AreEqual(new[] { false, true, false }, row.States());
            row.Reset(null);
            Assert.AreEqual(0, row.Total);
            Assert.AreEqual(0, row.Earned);
        }

        [Test]
        public void Rewards_LevelAndSummaryText()
        {
            Assert.AreEqual(RewardLevel.Calm, DialogueRewards.ParseLevel(""));
            Assert.AreEqual(RewardLevel.Calm, DialogueRewards.ParseLevel(null));
            Assert.AreEqual(RewardLevel.Calm, DialogueRewards.ParseLevel("7"));
            Assert.AreEqual(RewardLevel.Lively, DialogueRewards.ParseLevel("lively"));
            Assert.AreEqual(RewardLevel.Quiet, DialogueRewards.ParseLevel(" Quiet "));
            Assert.AreEqual("You answered 1 question!", DialogueRewards.SummaryText(1));
            Assert.AreEqual("You answered 7 questions!", DialogueRewards.SummaryText(7));
        }

        [Test]
        public void Rewards_ChimesAreShortAndNotLoud()
        {
            foreach (bool lively in new[] { false, true })
            {
                AudioClip clip = DialogueRewards.Chime(lively);
                Assert.Less(clip.length, 1.6f);
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                float peak = 0f;
                foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                Assert.Greater(peak, 0.05f);
                Assert.Less(peak, 0.7f, "never close to full volume");
            }
        }

        // ---- WantsMicrophone (the question for the grown-up before the book) ----

        private const string PageWithQuestion = "////////[chunk_1]\nDialogueQuestion \"Is it big?\", \"\"\nDialogueShow\n";

        [Test]
        public void WantsMicrophone_BookWithoutQuestions_No()
        {
            Assert.IsFalse(DialogueScript.WantsMicrophone("GoTo(\"Next\")\n////////[chunk_1]\nPlayAudioAndText \"a\", \"b\"\n"));
            Assert.IsFalse(DialogueScript.WantsMicrophone(""));
            Assert.IsFalse(DialogueScript.WantsMicrophone(null));
        }

        [Test]
        public void WantsMicrophone_QuestionWithTheDefaultInput_Yes()
        {
            Assert.IsTrue(DialogueScript.WantsMicrophone(PageWithQuestion));
        }

        [Test]
        public void WantsMicrophone_BookSetToTouch_No()
        {
            Assert.IsFalse(DialogueScript.WantsMicrophone("DialogueInput \"touch\"\n" + PageWithQuestion));
        }

        [Test]
        public void WantsMicrophone_BookSetToTouch_OnePageListens_Yes()
        {
            string script = "DialogueInput \"touch\"\n" + PageWithQuestion +
                            "////////[chunk_2]\nDialogueQuestion \"Say it!\", \"\"\nDialogueInput \"sound\"\nDialogueShow\n";
            Assert.IsTrue(DialogueScript.WantsMicrophone(script));
        }

        [Test]
        public void WantsMicrophone_OnlyTouchPages_No()
        {
            string script = "////////[chunk_1]\nDialogueInput(\"touch\")\nDialogueQuestion \"Is it big?\", \"\"\n" +
                            "////////[event OnAnswer\nScriptLog \"x\"\n" +
                            "////////[chunk_2]\n// DialogueQuestion \"commented out\", \"\"\n";
            Assert.IsFalse(DialogueScript.WantsMicrophone(script));
        }

        // ---- SoundTrigger ("any sound counts") ----

        private static bool FeedFor(SoundTrigger t, float level, float seconds)
        {
            bool fired = false;
            for (float time = 0f; time < seconds - 0.0001f; time += 0.02f) fired |= t.Feed(level, 0.02f);
            return fired;
        }

        [Test]
        public void Sound_QuietRoom_VoiceTriggers()
        {
            var t = new SoundTrigger();
            Assert.IsFalse(FeedFor(t, 0.004f, 0.5f), "measuring the room");
            Assert.IsFalse(FeedFor(t, 0.004f, 1f), "room noise is not an answer");
            Assert.IsTrue(FeedFor(t, 0.1f, 0.3f));
        }

        [Test]
        public void Sound_ShortClick_DoesNotTrigger()
        {
            var t = new SoundTrigger();
            FeedFor(t, 0.004f, 0.5f);
            Assert.IsFalse(FeedFor(t, 0.5f, 0.1f));
            Assert.IsFalse(FeedFor(t, 0.004f, 0.2f));
            Assert.IsFalse(FeedFor(t, 0.5f, 0.1f), "two short clicks do not add up");
        }

        [Test]
        public void Sound_NoisyRoom_NeedsALouderSound()
        {
            var t = new SoundTrigger();
            FeedFor(t, 0.02f, 0.5f);
            Assert.AreEqual(0.06f, t.Threshold, 0.001f);
            Assert.IsFalse(FeedFor(t, 0.05f, 0.5f));
            Assert.IsTrue(FeedFor(t, 0.3f, 0.3f));
        }

        [Test]
        public void Sound_VeryNoisyRoom_ALoudSoundStillCounts()
        {
            var t = new SoundTrigger();
            FeedFor(t, 0.06f, 0.5f);
            Assert.AreEqual(t.loudLevel, t.Threshold, "the trigger level has a ceiling");
            Assert.IsTrue(FeedFor(t, 0.3f, 0.3f));
        }

        [Test]
        public void Sound_VeryQuietRoom_StillNeedsARealSound()
        {
            var t = new SoundTrigger();
            FeedFor(t, 0f, 0.5f);
            Assert.AreEqual(t.minLevel, t.Threshold);
            Assert.IsFalse(FeedFor(t, 0.01f, 0.5f));
        }

        [Test]
        public void Sound_LoudSoundDuringTheRoomMeasurement_Counts()
        {
            // An eager child answers before the room has been measured.
            var t = new SoundTrigger();
            Assert.IsTrue(FeedFor(t, 0.3f, 0.4f));
        }

        [Test]
        public void Sound_SoftSoundDuringTheRoomMeasurement_DoesNotTrigger()
        {
            var t = new SoundTrigger();
            Assert.IsFalse(FeedFor(t, 0.05f, 0.4f));
            Assert.IsTrue(t.MeasuringRoom);
        }

        [Test]
        public void Sound_RoomMeasuredTooHigh_ComesDownInQuietMoments()
        {
            // The child hummed while the room was measured, then went quiet.
            var t = new SoundTrigger();
            FeedFor(t, 0.03f, 0.5f);
            Assert.AreEqual(0.09f, t.Threshold, 0.001f);
            FeedFor(t, 0.004f, 4f);
            Assert.Less(t.Threshold, 0.03f);
            Assert.IsTrue(FeedFor(t, 0.05f, 0.3f), "a soft voice is now heard");
        }

        [Test]
        public void Sound_OneLongFrame_IsNotASound()
        {
            var t = new SoundTrigger();
            FeedFor(t, 0.004f, 0.5f);
            Assert.IsFalse(t.Feed(0.5f, 3f), "a frame that took seconds counts as one short step");
        }
    }
}
