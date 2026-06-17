using NUnit.Framework;

// EditMode tests for Globals.WithContentRev — the ?v=<rev> media cache-buster.
// Lives in an Editor folder so it compiles into Assembly-CSharp-Editor and can see
// the predefined Assembly-CSharp game code (Globals) without an asmdef.
namespace ReadingBuddy.Tests
{
    public class GlobalsWithContentRevTests
    {
        [Test]
        public void NormalHttpUrl_WithRev_GetsVersionQuery()
        {
            Assert.AreEqual(
                "http://cdn/img.jpg?v=abc123",
                Globals.WithContentRev("http://cdn/img.jpg", "abc123"));
        }

        [Test]
        public void HttpsUrl_WithRev_GetsVersionQuery()
        {
            Assert.AreEqual(
                "https://cdn/a/b.mp3?v=deadbeef",
                Globals.WithContentRev("https://cdn/a/b.mp3", "deadbeef"));
        }

        [Test]
        public void EmptyRev_LeavesUrlUnchanged()
        {
            Assert.AreEqual("http://cdn/img.jpg", Globals.WithContentRev("http://cdn/img.jpg", ""));
            Assert.AreEqual("http://cdn/img.jpg", Globals.WithContentRev("http://cdn/img.jpg", null));
        }

        [Test]
        public void NonHttpUrl_LeavesUrlUnchanged()
        {
            // Relative paths and resources: urls must not be rewritten.
            Assert.AreEqual("img.jpg", Globals.WithContentRev("img.jpg", "abc"));
            Assert.AreEqual("resources:cover", Globals.WithContentRev("resources:cover", "abc"));
        }

        [Test]
        public void UrlWithExistingQuery_LeavesUrlUnchanged_Idempotent()
        {
            // Already busted (or otherwise has a query) → left alone, so a second wrap is a no-op.
            Assert.AreEqual(
                "http://cdn/img.jpg?v=abc",
                Globals.WithContentRev("http://cdn/img.jpg?v=abc", "xyz"));
            Assert.AreEqual(
                "http://cdn/img.jpg?foo=1",
                Globals.WithContentRev("http://cdn/img.jpg?foo=1", "abc"));
        }

        [Test]
        public void EmptyOrNullUrl_LeavesUrlUnchanged()
        {
            Assert.AreEqual("", Globals.WithContentRev("", "abc"));
            Assert.AreEqual(null, Globals.WithContentRev(null, "abc"));
        }
    }
}
