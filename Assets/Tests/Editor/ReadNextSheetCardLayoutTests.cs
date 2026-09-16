using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// EditMode tests for the end-of-book sheet's next-book card layout, from the 2026-09-15 Simulator
// bug hunt: a giant blank green block sat under the card (reproduced in the Editor at 16:9 too).
// The card's VerticalLayoutGroup had childForceExpandHeight on while the card itself carried
// LayoutElement.flexibleHeight = 1; Unity's force-expand treats every child's flexible height as
// max(flexible, 1), so the 13px accent strip took a share of the card's spare height and rendered
// as a slab. The Row is the child that must absorb that height instead.
namespace ReadingBuddy.Tests
{
    public class ReadNextSheetCardLayoutTests
    {
        private GameObject _card, _row, _accent;

        [SetUp]
        public void SetUp()
        {
            _card = new GameObject("NextCard", typeof(RectTransform), typeof(VerticalLayoutGroup));
            _row = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement));
            _accent = new GameObject("Accent", typeof(RectTransform), typeof(LayoutElement));
            _row.transform.SetParent(_card.transform, false);
            _accent.transform.SetParent(_card.transform, false);

            // Anything a force-expand would have to override: the accent starts out "hungry".
            _accent.GetComponent<LayoutElement>().flexibleHeight = 1f;

            ReadNextSheet.ApplyCardLayout(
                _card.GetComponent<VerticalLayoutGroup>(),
                _row.GetComponent<LayoutElement>(),
                _accent.GetComponent<LayoutElement>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_card != null) Object.DestroyImmediate(_card);
        }

        [Test]
        public void CardGroup_DoesNotForceExpandChildHeights()
        {
            Assert.IsFalse(_card.GetComponent<VerticalLayoutGroup>().childForceExpandHeight,
                "force-expand hands the accent strip a share of the card's spare height");
        }

        [Test]
        public void CardGroup_StillControlsChildHeights()
        {
            Assert.IsTrue(_card.GetComponent<VerticalLayoutGroup>().childControlHeight);
        }

        [Test]
        public void Accent_TakesNoSpareHeight()
        {
            Assert.AreEqual(0f, _accent.GetComponent<LayoutElement>().flexibleHeight);
        }

        [Test]
        public void Accent_KeepsItsStripHeight()
        {
            Assert.AreEqual(13f, _accent.GetComponent<LayoutElement>().preferredHeight);
        }

        [Test]
        public void Row_AbsorbsTheSpareHeight()
        {
            Assert.AreEqual(1f, _row.GetComponent<LayoutElement>().flexibleHeight);
        }

        [Test]
        public void NullComponents_AreTolerated()
        {
            Assert.DoesNotThrow(() => ReadNextSheet.ApplyCardLayout(null, null, null));
        }
    }
}
