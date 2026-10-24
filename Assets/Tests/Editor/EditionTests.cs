using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// EditMode tests for Edition: the parser, the choice by app identifier, and the edition files
// that ship in Resources/Editions.
namespace ReadingBuddy.Tests
{
    public class EditionTests
    {
        private static Edition Make(string id, params string[] identifiers) =>
            new Edition { id = id, appName = id, appIdentifiers = identifiers };

        private static List<Edition> ShippedEditions()
        {
            var list = new List<Edition>();
            foreach (TextAsset file in Resources.LoadAll<TextAsset>(Edition.ResourcesFolder))
            {
                Edition e = Edition.Parse(file.text);
                Assert.IsNotNull(e, $"Resources/{Edition.ResourcesFolder}/{file.name} is not a valid edition file");
                list.Add(e);
            }
            return list;
        }

        // ---- Parse ----

        [Test]
        public void Parse_ReadsAllFields()
        {
            Edition e = Edition.Parse(
                "{\"id\":\"x\",\"appName\":\"X App\",\"appIdentifiers\":[\"com.a.x\",\"com.b.x\"]," +
                "\"appStoreId\":\"123\",\"catalogUrl\":\"https://cdn/x/stories.json\"}");
            Assert.AreEqual("x", e.id);
            Assert.AreEqual("X App", e.appName);
            CollectionAssert.AreEqual(new[] { "com.a.x", "com.b.x" }, e.appIdentifiers);
            Assert.AreEqual("123", e.appStoreId);
            Assert.AreEqual("https://cdn/x/stories.json", e.catalogUrl);
        }

        [Test]
        public void Parse_MissingOptionalFields_GetEmptyDefaults()
        {
            Edition e = Edition.Parse("{\"id\":\"x\"}");
            Assert.AreEqual("", e.appName);
            Assert.AreEqual("", e.appStoreId);
            Assert.AreEqual("", e.catalogUrl);
            Assert.IsNotNull(e.appIdentifiers);
            Assert.AreEqual(0, e.appIdentifiers.Length);
        }

        [Test]
        public void Parse_UnknownFields_AreIgnored()
        {
            Assert.AreEqual("x", Edition.Parse("{\"id\":\"x\",\"somethingNew\":5}").id);
        }

        [Test]
        public void Parse_NotAnEdition_ReturnsNull()
        {
            Assert.IsNull(Edition.Parse(null));
            Assert.IsNull(Edition.Parse(""));
            Assert.IsNull(Edition.Parse("   "));
            Assert.IsNull(Edition.Parse("not json"));
            Assert.IsNull(Edition.Parse("{\"appName\":\"no id\"}"));
        }

        // ---- Pick ----

        [Test]
        public void Pick_ReturnsTheEditionThatListsTheIdentifier()
        {
            var all = new[] { Make(Edition.DefaultId, "com.a.reader"), Make("speech", "com.a.speech", "com.a.speech.android") };
            Assert.AreEqual("speech", Edition.Pick(all, "com.a.speech.android").id);
            Assert.AreEqual(Edition.DefaultId, Edition.Pick(all, "com.a.reader").id);
        }

        [Test]
        public void Pick_IgnoresLetterCase()
        {
            var all = new[] { Make(Edition.DefaultId, "com.a.reader"), Make("speech", "com.Imagi.Speech-App") };
            Assert.AreEqual("speech", Edition.Pick(all, "com.imagi.speech-app").id);
        }

        [Test]
        public void Pick_UnknownIdentifier_FallsBackToTheDefaultEdition()
        {
            var all = new[] { Make("speech", "com.a.speech"), Make(Edition.DefaultId, "com.a.reader") };
            Assert.AreEqual(Edition.DefaultId, Edition.Pick(all, "com.somebody.else").id);
            Assert.AreEqual(Edition.DefaultId, Edition.Pick(all, null).id);
        }

        [Test]
        public void Pick_NoMatchAndNoDefault_ReturnsNull()
        {
            Assert.IsNull(Edition.Pick(new[] { Make("speech", "com.a.speech") }, "com.somebody.else"));
            Assert.IsNull(Edition.Pick(new Edition[0], "com.a.reader"));
        }

        [Test]
        public void Pick_SkipsNullEntries()
        {
            var all = new[] { null, Make(Edition.DefaultId, "com.a.reader") };
            Assert.AreEqual(Edition.DefaultId, Edition.Pick(all, "com.a.reader").id);
        }

        // ---- the edition files that ship ----

        [Test]
        public void Shipped_DefaultEditionExists()
        {
            Assert.IsTrue(ShippedEditions().Exists(e => e.id == Edition.DefaultId));
        }

        [Test]
        public void Shipped_EveryEditionIsComplete()
        {
            foreach (Edition e in ShippedEditions())
            {
                Assert.IsNotEmpty(e.appName, e.id + ": appName");
                Assert.IsNotEmpty(e.appIdentifiers, e.id + ": appIdentifiers");
                StringAssert.StartsWith("https://", e.catalogUrl, e.id + ": catalogUrl");
                StringAssert.IsMatch(@"/stories(\.[a-z0-9]+)?\.json$", e.catalogUrl, e.id + ": catalogUrl");
            }
        }

        [Test]
        public void Shipped_IdsAndIdentifiersAreNotShared()
        {
            var ids = new HashSet<string>();
            var identifiers = new HashSet<string>();
            foreach (Edition e in ShippedEditions())
            {
                Assert.IsTrue(ids.Add(e.id), "edition id used twice: " + e.id);
                foreach (string identifier in e.appIdentifiers)
                    Assert.IsTrue(identifiers.Add(identifier.ToLowerInvariant()), "app identifier used twice: " + identifier);
            }
        }

        // The test edition: its own identifier and its own catalog, so that a test build can
        // never show production users a test book, and never be taken for the store app.
        [Test]
        public void Shipped_TestEdition_HasItsOwnIdentifierAndCatalog()
        {
            List<Edition> all = ShippedEditions();
            Edition prod = all.Find(e => e.id == Edition.DefaultId);
            Edition test = all.Find(e => e.id == "readingbuddy-test");
            Assert.IsNotNull(test, "Resources/Editions/readingbuddy-test.json");
            Assert.AreNotEqual(prod.catalogUrl, test.catalogUrl);
            Assert.AreNotEqual(prod.appName, test.appName);
            Assert.AreSame(test, Edition.Pick(all, "com.imagiration.readingteacher.test"));
            Assert.AreSame(prod, Edition.Pick(all, "com.imagiration.readingteacher"));
            // Both catalogs sit in the same folder: the book folders are shared.
            Assert.AreEqual(PRUtils.RemoveFileNameFromUrl(prod.catalogUrl), PRUtils.RemoveFileNameFromUrl(test.catalogUrl));
        }

        [Test]
        public void Shipped_TalkEdition_ShowsOnlyQuestionBooks_AndHasItsOwnHome()
        {
            List<Edition> all = ShippedEditions();
            Edition prod = all.Find(e => e.id == Edition.DefaultId);
            Edition talk = all.Find(e => e.id == "story-comprehension");
            Assert.IsNotNull(talk, "Resources/Editions/story-comprehension.json");
            Assert.IsTrue(talk.questionBooksOnly);
            Assert.IsFalse(prod.questionBooksOnly, "ReadingBuddy shows every book");
            Assert.AreEqual("_HomeTalk", talk.homeScene);
            Assert.AreEqual("_Home", prod.homeScene, "a missing homeScene means ReadingBuddy's Home");
            Assert.IsFalse(talk.wordTapWhileReading, "no word taps while the page is read");
            Assert.IsTrue(prod.wordTapWhileReading, "a missing wordTapWhileReading keeps ReadingBuddy's word taps");
            Assert.IsFalse(talk.puzzle, "no puzzle button in Story Comprehension");
            Assert.IsTrue(prod.puzzle, "a missing puzzle keeps ReadingBuddy's puzzle button");
            Assert.IsTrue(talk.downloadAllBooks, "Story Comprehension keeps every book of the level on the phone");
            Assert.IsFalse(prod.downloadAllBooks, "ReadingBuddy downloads books as they are opened");
            Assert.IsFalse(talk.readingSpeedByAgeGroup, "Story Comprehension: the slider alone sets the speed");
            Assert.AreEqual("-10", talk.defaultReadingRate);
            Assert.IsTrue(prod.readingSpeedByAgeGroup, "a missing readingSpeedByAgeGroup keeps ReadingBuddy's rule");
            Assert.AreEqual("0", prod.defaultReadingRate);
            Assert.AreSame(talk, Edition.Pick(all, "com.imagiration.storycomprehension"));
            Assert.AreEqual(PRUtils.RemoveFileNameFromUrl(prod.catalogUrl), PRUtils.RemoveFileNameFromUrl(talk.catalogUrl));
        }

        // The project settings decide which product a build is. They must name an edition directly
        // (not through the fallback), and the product name must be that edition's name.
        [Test]
        public void ProjectSettings_BelongToOneEdition()
        {
            List<Edition> all = ShippedEditions();
            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.iOS, NamedBuildTarget.Android })
            {
                string identifier = PlayerSettings.GetApplicationIdentifier(target);
                Edition e = all.Find(x => System.Array.Exists(x.appIdentifiers,
                    i => string.Equals(i, identifier, System.StringComparison.OrdinalIgnoreCase)));
                Assert.IsNotNull(e, $"no edition lists the {target.TargetName} identifier {identifier}");
                Assert.AreEqual(PlayerSettings.productName, e.appName, $"{target.TargetName}: product name");
            }
        }

        // A catalog address typed into the Globals prefab overrides the edition (for a local server).
        // It must not be committed or shipped that way.
        [Test]
        public void GlobalsPrefab_HasNoCatalogOverride()
        {
            var prefab = Resources.Load<GameObject>("Globals");
            Assert.IsNotNull(prefab, "Resources/Globals.prefab");
            Assert.IsEmpty(prefab.GetComponent<Globals>().csvUrl,
                "Globals.prefab csvUrl must be empty; the catalog address comes from the edition file");
        }
    }
}
