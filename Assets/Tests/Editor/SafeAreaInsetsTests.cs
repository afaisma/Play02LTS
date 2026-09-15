using NUnit.Framework;
using UnityEngine;

// EditMode tests for the pure half of SafeAreaInsets: reading Screen.safeArea (Unity's rect has a
// BOTTOM-LEFT origin) and the CanvasScaler scale factor. The Apply* side needs live RectTransforms
// and is exercised by the iOS Simulator smoke instead.
//
// The four fixtures are the real numbers the smoke devices report in Recordings/simcaps/sim/*_log.txt.
namespace ReadingBuddy.Tests
{
    public class SafeAreaInsetsTests
    {
        // "safe 0,102,1320,2580" on a 1320x2868 screen — Dynamic Island + home indicator.
        private static readonly Rect Phone17ProMax = new Rect(0f, 102f, 1320f, 2580f);
        // "safe 0,0,750,1334" — no insets at all.
        private static readonly Rect PhoneSE = new Rect(0f, 0f, 750f, 1334f);
        // "safe 0,50,1488,2216" on 1488x2266 — home indicator only (the status bar is hidden).
        private static readonly Rect IPadMini = new Rect(0f, 50f, 1488f, 2216f);

        [Test]
        public void Pixels_ReadsBottomLeftOrigin_OnANotchedPhone()
        {
            var e = SafeAreaInsets.Pixels(Phone17ProMax, 1320f, 2868f);
            Assert.AreEqual(102f, e.Bottom, 0.01f, "safeArea.y is the BOTTOM inset");
            Assert.AreEqual(186f, e.Top, 0.01f, "top inset is height - yMax");
            Assert.AreEqual(0f, e.Left, 0.01f);
            Assert.AreEqual(0f, e.Right, 0.01f);
        }

        [Test]
        public void Pixels_OnAnIPad_IsBottomOnly()
        {
            var e = SafeAreaInsets.Pixels(IPadMini, 1488f, 2266f);
            Assert.AreEqual(50f, e.Bottom, 0.01f);
            Assert.AreEqual(0f, e.Top, 0.01f, "the app hides the status bar, so an iPad has no top inset");
        }

        [Test]
        public void Pixels_WithNoInsets_IsZero()
        {
            Assert.IsTrue(SafeAreaInsets.Pixels(PhoneSE, 750f, 1334f).IsZero);
        }

        [Test]
        public void Pixels_OnADegenerateScreen_IsZero()
        {
            Assert.IsTrue(SafeAreaInsets.Pixels(Phone17ProMax, 0f, 0f).IsZero);
            Assert.IsTrue(SafeAreaInsets.Pixels(new Rect(0f, 0f, 0f, 0f), 1320f, 2868f).IsZero);
        }

        [Test]
        public void Pixels_ClampsASafeAreaLargerThanTheScreen()
        {
            var e = SafeAreaInsets.Pixels(new Rect(-10f, -10f, 1340f, 2888f), 1320f, 2868f);
            Assert.IsTrue(e.IsZero, "a simulator reporting an oversized safe area must not produce negative insets");
        }

        [Test]
        public void TopWithMargin_AddsBreathingRoomOnlyWhenThereIsATopInset()
        {
            var notched = SafeAreaInsets.Pixels(Phone17ProMax, 1320f, 2868f);
            Assert.AreEqual(186f + SafeAreaInsets.TopBreathingMargin, notched.TopWithMargin, 0.01f);

            var flat = SafeAreaInsets.Pixels(PhoneSE, 750f, 1334f);
            Assert.AreEqual(0f, flat.TopWithMargin, 0.01f,
                "a device with no top inset must not move — the SE captures are the regression check");
        }

        [Test]
        public void ScaleFactor_AtTheReferenceResolution_IsOne()
        {
            Assert.AreEqual(1f, SafeAreaInsets.ScaleFactor(1080f, 1920f, SafeAreaInsets.Reference, 0.5f), 1e-4f);
        }

        [Test]
        public void ScaleFactor_IsTheGeometricBlendOfBothAxes()
        {
            // match 0.5 -> sqrt of the product of the two ratios.
            float expected = Mathf.Sqrt((1320f / 1080f) * (2868f / 1920f));
            Assert.AreEqual(expected, SafeAreaInsets.ScaleFactor(1320f, 2868f, SafeAreaInsets.Reference, 0.5f), 1e-4f);
        }

        [Test]
        public void ScaleFactor_AtMatchZeroAndOne_FollowsOneAxisEach()
        {
            Assert.AreEqual(1320f / 1080f,
                SafeAreaInsets.ScaleFactor(1320f, 2868f, SafeAreaInsets.Reference, 0f), 1e-4f);
            Assert.AreEqual(2868f / 1920f,
                SafeAreaInsets.ScaleFactor(1320f, 2868f, SafeAreaInsets.Reference, 1f), 1e-4f);
        }

        [Test]
        public void ScaleFactor_OnADegenerateInput_IsOne()
        {
            Assert.AreEqual(1f, SafeAreaInsets.ScaleFactor(0f, 2868f, SafeAreaInsets.Reference, 0.5f), 1e-4f);
            Assert.AreEqual(1f, SafeAreaInsets.ScaleFactor(1320f, 2868f, Vector2.zero, 0.5f), 1e-4f);
        }

        [Test]
        public void Divided_ConvertsPixelsToCanvasUnits()
        {
            float scale = SafeAreaInsets.ScaleFactor(1320f, 2868f, SafeAreaInsets.Reference, 0.5f);
            var canvas = SafeAreaInsets.Pixels(Phone17ProMax, 1320f, 2868f).Divided(scale);
            Assert.AreEqual(102f / scale, canvas.Bottom, 0.01f);
            Assert.AreEqual(186f / scale, canvas.Top, 0.01f);
            // Sanity: on this device the canvas is scaled up, so the inset shrinks in canvas units.
            Assert.Less(canvas.Bottom, 102f);
        }

        [Test]
        public void Divided_ByANonPositiveScale_IsZero()
        {
            Assert.IsTrue(SafeAreaInsets.Pixels(Phone17ProMax, 1320f, 2868f).Divided(0f).IsZero);
        }
    }
}
