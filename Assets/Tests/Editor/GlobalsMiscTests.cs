using NUnit.Framework;
using UnityEngine;

// EditMode tests for small Globals statics: age-group label, default audio rate, and PlayerPrefs keys.
namespace ReadingBuddy.Tests
{
    public class GlobalsMiscTests
    {
        private static PRBook BookAged(int ageFrom)
        {
            return new PRBook { ageFrom = ageFrom };
        }

        // ---- ageGroupLabelFromPRBook ----

        [Test]
        public void AgeGroupLabel_Age2() => Assert.AreEqual("2-4 years", Globals.ageGroupLabelFromPRBook(BookAged(2)));

        [Test]
        public void AgeGroupLabel_Age3() => Assert.AreEqual("3-6 years", Globals.ageGroupLabelFromPRBook(BookAged(3)));

        [Test]
        public void AgeGroupLabel_Age4() => Assert.AreEqual("4-8 years", Globals.ageGroupLabelFromPRBook(BookAged(4)));

        [Test]
        public void AgeGroupLabel_Age5() => Assert.AreEqual("5-12 years", Globals.ageGroupLabelFromPRBook(BookAged(5)));

        [Test]
        public void AgeGroupLabel_OtherAge_AnyAge() => Assert.AreEqual("Any Age", Globals.ageGroupLabelFromPRBook(BookAged(6)));

        // ---- defaultAudioRateFromPRBook ----

        [Test]
        public void DefaultAudioRate_Age2() => Assert.AreEqual(-20, Globals.defaultAudioRateFromPRBook(BookAged(2)));

        [Test]
        public void DefaultAudioRate_Age3() => Assert.AreEqual(-10, Globals.defaultAudioRateFromPRBook(BookAged(3)));

        [Test]
        public void DefaultAudioRate_Age4() => Assert.AreEqual(0, Globals.defaultAudioRateFromPRBook(BookAged(4)));

        [Test]
        public void DefaultAudioRate_Age5() => Assert.AreEqual(10, Globals.defaultAudioRateFromPRBook(BookAged(5)));

        [Test]
        public void DefaultAudioRate_NullBook_IsZero() => Assert.AreEqual(0, Globals.defaultAudioRateFromPRBook(null));

        [Test]
        public void DefaultAudioRate_OutOfRangeAge_FallsBackToMinus30_DocumentedWart()
        {
            // DOCUMENTED WART: any ageFrom outside {2,3,4,5} (e.g. 6 — a legitimate "ages 6+" book)
            // silently gets the slowest rate (-30), the same value as ageFrom==0/unknown. A child of
            // 6 is therefore narrated as slowly as a 2-year-old. If the rate table is ever extended,
            // FLIP this assertion.
            Assert.AreEqual(-30, Globals.defaultAudioRateFromPRBook(BookAged(6)));
        }

        // ---- PlayerPrefs key builders ----

        [Test]
        public void PrefsPageKey_AppendsPageSuffix()
        {
            Assert.AreEqual("abc_page", Globals.Prefs_BookUrl_To_Page_Key("abc"));
        }

        [Test]
        public void PrefsDoneKey_AppendsDoneSuffix()
        {
            Assert.AreEqual("abc_done", Globals.Prefs_BookUrl_To_BookDone_Key("abc"));
        }
    }
}
