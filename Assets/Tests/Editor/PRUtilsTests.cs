using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for the pure helpers in PRUtils.cs.
namespace ReadingBuddy.Tests
{
    public class PRUtilsTests
    {
        // ---- RemoveFileNameFromUrl ----

        [Test]
        public void RemoveFileNameFromUrl_NormalUrl_DropsFileKeepsTrailingSlash()
        {
            Assert.AreEqual("http://cdn.com/a/b/", PRUtils.RemoveFileNameFromUrl("http://cdn.com/a/b/file.txt"));
        }

        [Test]
        public void RemoveFileNameFromUrl_FileAtRoot_ReturnsAuthorityRoot()
        {
            Assert.AreEqual("http://cdn.com/", PRUtils.RemoveFileNameFromUrl("http://cdn.com/file.txt"));
        }

        [Test]
        public void RemoveFileNameFromUrl_Garbage_ReturnedUnchanged()
        {
            // new Uri(...) throws on an unparseable string; the catch returns the input verbatim.
            Assert.AreEqual("garbage", PRUtils.RemoveFileNameFromUrl("garbage"));
        }

        // ---- UrlUp ----

        [Test]
        public void UrlUp_OneStep_DropsLastSegment()
        {
            Assert.AreEqual("http://a.com/b/c", PRUtils.UrlUp("http://a.com/b/c/d", 1));
        }

        [Test]
        public void UrlUp_TwoSteps_DropsTwoSegments()
        {
            Assert.AreEqual("http://a.com/b", PRUtils.UrlUp("http://a.com/b/c/d", 2));
        }

        [Test]
        public void UrlUp_ZeroSteps_ReturnsUnchanged()
        {
            Assert.AreEqual("http://a.com/b/c/d", PRUtils.UrlUp("http://a.com/b/c/d", 0));
        }

        [Test]
        public void UrlUp_ExhaustedSlashes_ReturnsEmpty()
        {
            // "a/b" -> "a" -> no slash left -> "".
            Assert.AreEqual("", PRUtils.UrlUp("a/b", 5));
        }

        // ---- StringToColor / StringToColor1 ----

        [Test]
        public void StringToColor_HexString_ParsesRgb()
        {
            Color c = PRUtils.StringToColor("FF0000");
            Assert.That(c.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.g, Is.EqualTo(0f).Within(0.01f));
            Assert.That(c.b, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void StringToColor_CommaString_RoutesToRgbParser()
        {
            Color c = PRUtils.StringToColor("0,255,0");
            Assert.That(c.r, Is.EqualTo(0f).Within(0.01f));
            Assert.That(c.g, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.b, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void StringToColor1_ValidRgb_Parses()
        {
            Color c = PRUtils.StringToColor1("255,0,0");
            Assert.That(c.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.g, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void StringToColor1_WrongComponentCount_ReturnsWhite()
        {
            Assert.AreEqual(Color.white, PRUtils.StringToColor1("badformat"));
        }

        [Test]
        public void StringToColor1_NonNumericComponents_ReturnsWhite()
        {
            Assert.AreEqual(Color.white, PRUtils.StringToColor1("a,b,c"));
        }

        // ---- SplitStringIntoLines ----

        [Test]
        public void SplitStringIntoLines_Lf()
        {
            List<string> lines = PRUtils.SplitStringIntoLines("a\nb\nc");
            Assert.AreEqual(new[] { "a", "b", "c" }, lines.ToArray());
        }

        [Test]
        public void SplitStringIntoLines_CrLf()
        {
            List<string> lines = PRUtils.SplitStringIntoLines("a\r\nb");
            Assert.AreEqual(new[] { "a", "b" }, lines.ToArray());
        }

        [Test]
        public void SplitStringIntoLines_Cr()
        {
            List<string> lines = PRUtils.SplitStringIntoLines("a\rb");
            Assert.AreEqual(new[] { "a", "b" }, lines.ToArray());
        }

        // ---- CapitalizeFirstLetter ----

        [Test]
        public void CapitalizeFirstLetter_Normal()
        {
            Assert.AreEqual("Hello", PRUtils.CapitalizeFirstLetter("hello"));
        }

        [Test]
        public void CapitalizeFirstLetter_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PRUtils.CapitalizeFirstLetter(""));
        }

        [Test]
        public void CapitalizeFirstLetter_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PRUtils.CapitalizeFirstLetter(null));
        }

        // ---- Convert (number words) ----

        [Test]
        public void Convert_Zero() => Assert.AreEqual("Zero", PRUtils.Convert(0));

        [Test]
        public void Convert_SingleDigit() => Assert.AreEqual("Five", PRUtils.Convert(5));

        [Test]
        public void Convert_Teen() => Assert.AreEqual("Thirteen", PRUtils.Convert(13));

        [Test]
        public void Convert_Tens() => Assert.AreEqual("Twenty One", PRUtils.Convert(21));

        [Test]
        public void Convert_Hundred() => Assert.AreEqual("One Hundred", PRUtils.Convert(100));

        [Test]
        public void Convert_HundredsWithRemainder() => Assert.AreEqual("One Hundred and Twenty Three", PRUtils.Convert(123));

        [Test]
        public void Convert_Thousand() => Assert.AreEqual("One Thousand", PRUtils.Convert(1000));

        // ---- AlmostEqual ----

        [Test]
        public void AlmostEqual_WithinDelta_True()
        {
            Assert.IsTrue(PRUtils.AlmostEqual(1.0f, 1.05f, 0.1f));
        }

        [Test]
        public void AlmostEqual_OutsideDelta_False()
        {
            Assert.IsFalse(PRUtils.AlmostEqual(1.0f, 1.5f, 0.1f));
        }
    }
}
