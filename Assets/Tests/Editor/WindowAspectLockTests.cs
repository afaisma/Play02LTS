using NUnit.Framework;
using UnityEngine;

public class WindowAspectLockTests
{
    [Test]
    public void IsPortraitFit_AcceptsExact9x16AndSmallDrift()
    {
        Assert.IsTrue(WindowAspectLock.IsPortraitFit(720, 1280));
        Assert.IsTrue(WindowAspectLock.IsPortraitFit(724, 1280)); // ~0.5% drift
        Assert.IsFalse(WindowAspectLock.IsPortraitFit(1280, 720)); // landscape
        Assert.IsFalse(WindowAspectLock.IsPortraitFit(800, 1280)); // too wide
        Assert.IsFalse(WindowAspectLock.IsPortraitFit(0, 0));
    }

    [Test]
    public void FitPortrait_DrivesFromHeightWhenHeightChangedMost()
    {
        // user dragged the bottom edge: 1280 -> 1000, width untouched
        var fit = WindowAspectLock.FitPortrait(720, 1000, 720, 1280, 2000);
        Assert.AreEqual(1000, fit.y);
        Assert.AreEqual(Mathf.RoundToInt(1000 * WindowAspectLock.Aspect), fit.x);
    }

    [Test]
    public void FitPortrait_DrivesFromWidthWhenWidthChangedMost()
    {
        // user dragged the side edge: 720 -> 900, height untouched
        var fit = WindowAspectLock.FitPortrait(900, 1280, 720, 1280, 2000);
        Assert.AreEqual(Mathf.RoundToInt(900 / WindowAspectLock.Aspect), fit.y);
        Assert.AreEqual(Mathf.RoundToInt(fit.y * WindowAspectLock.Aspect), fit.x);
    }

    [Test]
    public void FitPortrait_ClampsToMinAndMaxHeight()
    {
        var small = WindowAspectLock.FitPortrait(100, 100, 720, 1280, 2000);
        Assert.AreEqual(WindowAspectLock.MinHeight, small.y);

        var tall = WindowAspectLock.FitPortrait(720, 5000, 720, 1280, 1400);
        Assert.AreEqual(1400, tall.y);
        Assert.IsTrue(WindowAspectLock.IsPortraitFit(tall.x, tall.y));
    }

    [Test]
    public void FitPortrait_NeverReturnsLandscape()
    {
        var fit = WindowAspectLock.FitPortrait(1920, 1080, 720, 1280, 1040);
        Assert.Less(fit.x, fit.y);
        Assert.IsTrue(WindowAspectLock.IsPortraitFit(fit.x, fit.y));
    }

    [Test]
    public void InitialSize_FitsDisplayAndIsCapped()
    {
        var hd = WindowAspectLock.InitialSize(1080);   // 1080p monitor -> 972 tall
        Assert.AreEqual(972, hd.y);
        Assert.IsTrue(WindowAspectLock.IsPortraitFit(hd.x, hd.y));

        var big = WindowAspectLock.InitialSize(2160);  // 4K -> capped at 1280
        Assert.AreEqual(WindowAspectLock.MaxDefaultHeight, big.y);

        var tiny = WindowAspectLock.InitialSize(600);  // never below MinHeight
        Assert.AreEqual(WindowAspectLock.MinHeight, tiny.y);
    }
}
