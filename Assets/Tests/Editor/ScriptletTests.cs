using NUnit.Framework;

// EditMode tests for Scriptlet.ParseTitleString / GetName (PRScript.cs) — parsing the
// "////////[chunk name=...]" header line that delimits each story page.
namespace ReadingBuddy.Tests
{
    public class ScriptletTests
    {
        [Test]
        public void GetName_ParsesNameFromChunkHeader()
        {
            var scriptlet = new Scriptlet("////////[chunk name=page1]");
            Assert.AreEqual("page1", scriptlet.GetName());
        }

        [Test]
        public void GetName_MissingName_ReturnsEmpty()
        {
            var scriptlet = new Scriptlet("////////[chunk]");
            Assert.AreEqual(string.Empty, scriptlet.GetName());
        }

        [Test]
        public void GetName_WithExtraKeyValuePairs_StillReturnsName()
        {
            var scriptlet = new Scriptlet("////////[chunk name=page2 foo=bar]");
            Assert.AreEqual("page2", scriptlet.GetName());
        }
    }
}
