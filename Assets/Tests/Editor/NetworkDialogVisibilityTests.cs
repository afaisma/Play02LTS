using NUnit.Framework;

// EditMode tests for the offline dialog's visibility rule (NetworkDialogVisibility). The rule
// exists because NetworkStatus polls the CDN every 5 seconds: without the dismiss latch, closing
// the dialog would only buy the user 5 seconds of quiet.
// Lives in an Editor folder so it compiles into Assembly-CSharp-Editor and can see the predefined
// Assembly-CSharp game code without an asmdef (same approach as the other tests here).
namespace ReadingBuddy.Tests
{
    public class NetworkDialogVisibilityTests
    {
        [Test]
        public void OfflinePoll_ShowsDialog()
        {
            var v = new NetworkDialogVisibility();
            Assert.IsTrue(v.OnStatusChange(false));
        }

        [Test]
        public void OnlinePoll_HidesDialog()
        {
            var v = new NetworkDialogVisibility();
            Assert.IsFalse(v.OnStatusChange(true));
        }

        [Test]
        public void RepeatedOfflinePolls_KeepShowingUntilDismissed()
        {
            var v = new NetworkDialogVisibility();
            Assert.IsTrue(v.OnStatusChange(false));
            Assert.IsTrue(v.OnStatusChange(false));
            Assert.IsTrue(v.OnStatusChange(false));
        }

        [Test]
        public void AfterDismiss_FurtherOfflinePollsStayQuiet()
        {
            var v = new NetworkDialogVisibility();
            v.OnStatusChange(false);
            v.Dismiss();
            Assert.IsTrue(v.Dismissed);
            Assert.IsFalse(v.OnStatusChange(false), "the 5s poll must not nag after a deliberate dismiss");
            Assert.IsFalse(v.OnStatusChange(false));
        }

        [Test]
        public void ConnectivityReturning_ClearsTheDismiss()
        {
            var v = new NetworkDialogVisibility();
            v.Dismiss();
            Assert.IsFalse(v.OnStatusChange(true));
            Assert.IsFalse(v.Dismissed);
        }

        [Test]
        public void AfterConnectivityComesBackAndDropsAgain_DialogReturns()
        {
            var v = new NetworkDialogVisibility();
            v.OnStatusChange(false);
            v.Dismiss();
            Assert.IsFalse(v.OnStatusChange(false), "still latched while offline");
            v.OnStatusChange(true);                                    // back online
            Assert.IsTrue(v.OnStatusChange(false), "a NEW drop is allowed to show the dialog again");
        }

        [Test]
        public void DismissTwice_IsHarmless()
        {
            var v = new NetworkDialogVisibility();
            v.Dismiss();
            v.Dismiss();
            Assert.IsFalse(v.OnStatusChange(false));
            v.OnStatusChange(true);
            Assert.IsTrue(v.OnStatusChange(false));
        }
    }
}
