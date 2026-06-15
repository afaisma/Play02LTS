using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for Globals.ParseJSON + Globals.ParseCatalog (the stories.json catalog
// migration — see CHANGE_STORIES_JSON_CATALOG.md / CATALOG_JSON_SPEC.md).
// Lives in an Editor folder so it compiles into Assembly-CSharp-Editor and can see the
// predefined Assembly-CSharp game code (Globals, PRBook, SimpleJSON) without an asmdef.
namespace ReadingBuddy.Tests
{
    public class GlobalsParseJSONTests
    {
        // Realistic fixture: three entries copied verbatim from the real generated catalog
        // at ~/dev/FileServer/uploads/stories/stories.json (schema_version 1, with the new
        // content_rev cache-busting field present), plus the top-level wrapper.
        private const string CatalogJson = @"
{
 ""schema_version"": 1,
 ""generated_at"": ""2026-06-11T17:56:02+00:00"",
 ""books"": [
  {
   ""name"": ""Alphabet Rhymebook"",
   ""author"": ""Dr. Stein"",
   ""cover"": ""Alphabet/images/cover.jpg"",
   ""script"": ""Alphabet/Alphabet_chunks_script.txt"",
   ""age_from"": 2,
   ""age_to"": 4,
   ""genres"": [ ""rhymebooks"", ""family"", ""special education"" ],
   ""notes_for_parents"": ""Learn animals"",
   ""id"": ""42"",
   ""store_url_printed"": """",
   ""store_url_kindle"": """",
   ""content_rev"": ""ad8d2b6bfb""
  },
  {
   ""name"": ""Jack and the Beanstalk"",
   ""author"": ""Alex Faisman"",
   ""cover"": ""JackAndTheBeanstalk_v2/images/thumb.jpg"",
   ""script"": ""JackAndTheBeanstalk_v2/jack_and_the_beanstalk_chunks_script.txt"",
   ""age_from"": 5,
   ""age_to"": 12,
   ""genres"": [ ""family"", ""adventure"", ""fairytales"" ],
   ""notes_for_parents"": ""Good for children"",
   ""id"": ""64"",
   ""store_url_printed"": ""https://amzn.to/47o9Sgb"",
   ""store_url_kindle"": ""https://amzn.to/47mfiZe"",
   ""content_rev"": ""dbf34aac8c""
  },
  {
   ""name"": ""The Snow Queen"",
   ""author"": ""Alex Faisman"",
   ""cover"": ""TheSnowQueen_v2/images/The_Snow_Queen_thumb.jpg"",
   ""script"": ""TheSnowQueen_v2/the_snow_queen_chunks_script.txt"",
   ""age_from"": 3,
   ""age_to"": 8,
   ""genres"": [ ""family"", ""art"", ""fairytales"" ],
   ""notes_for_parents"": ""Good for children"",
   ""id"": ""67"",
   ""store_url_printed"": ""https://amzn.to/45qfXGx"",
   ""store_url_kindle"": ""https://amzn.to/45HAzLB"",
   ""content_rev"": ""6bd24f2891""
  }
 ]
}";

        // The CSV form of the same three books, in the legacy column order, with the genres
        // joined by " : " — the equivalence partner for the happy-path test. This mirrors
        // what the generator's stories.csv (rollback artifact) emits.
        private const string CsvHeader = "name,author,image,url,ageFrom,ageTo,genre,notes,id,printed,kindle";
        private const string CatalogCsv = CsvHeader +
            "\nAlphabet Rhymebook,Dr. Stein,Alphabet/images/cover.jpg,Alphabet/Alphabet_chunks_script.txt,2,4,rhymebooks : family : special education,Learn animals,42,," +
            "\nJack and the Beanstalk,Alex Faisman,JackAndTheBeanstalk_v2/images/thumb.jpg,JackAndTheBeanstalk_v2/jack_and_the_beanstalk_chunks_script.txt,5,12,family : adventure : fairytales,Good for children,64,https://amzn.to/47o9Sgb,https://amzn.to/47mfiZe" +
            "\nThe Snow Queen,Alex Faisman,TheSnowQueen_v2/images/The_Snow_Queen_thumb.jpg,TheSnowQueen_v2/the_snow_queen_chunks_script.txt,3,8,family : art : fairytales,Good for children,67,https://amzn.to/45qfXGx,https://amzn.to/45HAzLB";

        private string _origBaseURL;

        [SetUp]
        public void SetUp()
        {
            // ParseJSON/ParseCSV read the static Globals.baseURL when resolving relative urls.
            _origBaseURL = Globals.baseURL;
            Globals.baseURL = "http://cdn/";
            ClearTestPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            Globals.baseURL = _origBaseURL;
            ClearTestPrefs();
        }

        private void ClearTestPrefs()
        {
            // Targeted clears (don't wipe the dev's prefs) for every script-url the fixtures use.
            foreach (string url in new[]
            {
                "Alphabet/Alphabet_chunks_script.txt",
                "JackAndTheBeanstalk_v2/jack_and_the_beanstalk_chunks_script.txt",
                "TheSnowQueen_v2/the_snow_queen_chunks_script.txt",
                "a/script.txt", "b/script.txt", "good/script.txt",
            })
            {
                PlayerPrefs.DeleteKey(Globals.Prefs_BookUrl_To_Page_Key(url));
                PlayerPrefs.DeleteKey(Globals.Prefs_BookUrl_To_BookDone_Key(url));
            }
        }

        // --- Happy path: full catalog JSON parses to the same count/fields as the CSV ---

        [Test]
        public void FullCatalog_ParsesSameCountAndFields_AsEquivalentCsv()
        {
            List<PRBook> fromJson = Globals.ParseJSON(CatalogJson);
            List<PRBook> fromCsv = Globals.ParseCSV(CatalogCsv);

            Assert.AreEqual(3, fromJson.Count);
            Assert.AreEqual(fromCsv.Count, fromJson.Count);

            for (int i = 0; i < fromJson.Count; i++)
            {
                PRBook j = fromJson[i];
                PRBook c = fromCsv[i];
                Assert.AreEqual(c.bookName, j.bookName, $"bookName at {i}");
                Assert.AreEqual(c.bookAuthor, j.bookAuthor, $"bookAuthor at {i}");
                Assert.AreEqual(c.bookImageUrl, j.bookImageUrl, $"bookImageUrl at {i}");
                Assert.AreEqual(c.bookUrl, j.bookUrl, $"bookUrl at {i}");
                Assert.AreEqual(c.ageFrom, j.ageFrom, $"ageFrom at {i}");
                Assert.AreEqual(c.ageTo, j.ageTo, $"ageTo at {i}");
                Assert.AreEqual(c.genre, j.genre, $"genre at {i}");
                Assert.AreEqual(c.notesForParents, j.notesForParents, $"notesForParents at {i}");
                Assert.AreEqual(c.id, j.id, $"id at {i}");
                Assert.AreEqual(c.bookStoreUrlPrinted, j.bookStoreUrlPrinted, $"printed at {i}");
                Assert.AreEqual(c.bookStoreUrlKindle, j.bookStoreUrlKindle, $"kindle at {i}");
                Assert.AreEqual(c.bookFullUrl, j.bookFullUrl, $"bookFullUrl at {i}");
                Assert.AreEqual(i, j.number, $"number at {i}");
            }
        }

        // --- bookUrl verbatim preservation (PlayerPrefs progress-key invariant) ---

        [Test]
        public void BookUrl_IsScriptString_Verbatim()
        {
            List<PRBook> books = Globals.ParseJSON(CatalogJson);

            Assert.AreEqual("Alphabet/Alphabet_chunks_script.txt", books[0].bookUrl);
            Assert.AreEqual("JackAndTheBeanstalk_v2/jack_and_the_beanstalk_chunks_script.txt", books[1].bookUrl);
            Assert.AreEqual("TheSnowQueen_v2/the_snow_queen_chunks_script.txt", books[2].bookUrl);
        }

        [Test]
        public void BookUrl_WithDoubleSlashAndSpacing_PreservedExactly()
        {
            // The invariant is byte-identical preservation of the script string, including
            // any double-slash or whitespace quirks — those keys must survive from the CSV era.
            string quirky = "stories//odd path/the script.txt";
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""Q"", ""author"": ""A"", ""cover"": ""c.jpg"",
                ""script"": """ + quirky + @""", ""age_from"": 3, ""age_to"": 6,
                ""genres"": [ ""family"" ], ""notes_for_parents"": ""n"", ""id"": ""1"" } ] }";

            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual(quirky, books[0].bookUrl);
            // And it must be the exact key PlayerPrefs progress is read/written under.
            Assert.AreEqual(quirky + "_page", Globals.Prefs_BookUrl_To_Page_Key(books[0].bookUrl));
        }

        // --- Malformed book entry skipped, remainder loads (C2 parity) ---

        [Test]
        public void MalformedBook_IsSkipped_RemainingBooksStillParse()
        {
            // Middle entry's "books" element is a bare number, not an object — indexing it
            // for fields throws inside the per-book try/catch, so it's skipped (C2 behavior).
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""Good A"", ""author"": ""A"", ""cover"": ""c"", ""script"": ""a/script.txt"",
                ""age_from"": 3, ""age_to"": 6, ""genres"": [ ""g"" ], ""notes_for_parents"": ""n"", ""id"": ""A"" },
              12345,
              { ""name"": ""Good B"", ""author"": ""A"", ""cover"": ""c"", ""script"": ""b/script.txt"",
                ""age_from"": 4, ""age_to"": 8, ""genres"": [ ""g"" ], ""notes_for_parents"": ""n"", ""id"": ""B"" } ] }";

            // The skipped entry logs a warning (not an error), which does not fail the test.
            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual(2, books.Count);
            Assert.AreEqual("Good A", books[0].bookName);
            Assert.AreEqual("Good B", books[1].bookName);
            // number stays contiguous across the skipped entry (counter only advances on success).
            Assert.AreEqual(0, books[0].number);
            Assert.AreEqual(1, books[1].number);
        }

        // --- Unknown fields ignored; missing optional fields → CSV-path defaults ---

        [Test]
        public void UnknownFieldsIgnored_MissingOptionalFields_GetCsvDefaults()
        {
            // Only required-ish fields present; store urls, level, read_to_me, content_rev,
            // plus a totally unknown field — all either defaulted or ignored.
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""Minimal"", ""author"": ""A"", ""cover"": ""c.jpg"", ""script"": ""good/script.txt"",
                ""age_from"": 3, ""age_to"": 6, ""genres"": [ ""family"" ], ""notes_for_parents"": ""n"", ""id"": ""9"",
                ""level"": 1, ""read_to_me"": true, ""content_rev"": ""deadbeef"", ""totally_unknown"": { ""x"": 1 } } ] }";

            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual(1, books.Count);
            PRBook b = books[0];
            // Missing optional store urls default to "" exactly like the CSV path.
            Assert.AreEqual("", b.bookStoreUrlPrinted);
            Assert.AreEqual("", b.bookStoreUrlKindle);
            // Known fields still mapped despite the unknown extras.
            Assert.AreEqual("Minimal", b.bookName);
            Assert.AreEqual("9", b.id);
        }

        [Test]
        public void MissingGenres_DefaultsToEmptyString()
        {
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""NoGenre"", ""author"": ""A"", ""cover"": ""c"", ""script"": ""good/script.txt"",
                ""age_from"": 3, ""age_to"": 6, ""notes_for_parents"": ""n"", ""id"": ""1"" } ] }";

            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("", books[0].genre);
        }

        [Test]
        public void NewerSchemaVersion_IsParsedNotRejected()
        {
            string json = @"{ ""schema_version"": 99, ""books"": [
              { ""name"": ""Future"", ""author"": ""A"", ""cover"": ""c"", ""script"": ""good/script.txt"",
                ""age_from"": 3, ""age_to"": 6, ""genres"": [ ""family"" ], ""notes_for_parents"": ""n"", ""id"": ""1"" } ] }";

            // The newer-version warning is a warning (not an error), so it does not fail the test.
            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("Future", books[0].bookName);
        }

        // --- genres array → legacy " : " joined string ---

        [Test]
        public void GenresArray_JoinsWithLegacySeparator()
        {
            List<PRBook> books = Globals.ParseJSON(CatalogJson);

            Assert.AreEqual("rhymebooks : family : special education", books[0].genre);
            Assert.AreEqual("family : adventure : fairytales", books[1].genre);
            Assert.AreEqual("family : art : fairytales", books[2].genre);
        }

        [Test]
        public void SingleGenre_HasNoSeparator()
        {
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""One"", ""author"": ""A"", ""cover"": ""c"", ""script"": ""good/script.txt"",
                ""age_from"": 3, ""age_to"": 6, ""genres"": [ ""family"" ], ""notes_for_parents"": ""n"", ""id"": ""1"" } ] }";

            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual("family", books[0].genre);
        }

        // --- Extension dispatch: .json vs .csv routes to the right parser ---

        [Test]
        public void ParseCatalog_JsonExtension_RoutesToParseJSON()
        {
            // A .json url parsed as CSV would fail (the JSON braces aren't CSV columns);
            // getting the three well-formed books back proves it routed to ParseJSON.
            List<PRBook> books = Globals.ParseCatalog("http://cdn/stories-qa/stories.json", CatalogJson);

            Assert.AreEqual(3, books.Count);
            Assert.AreEqual("Alphabet Rhymebook", books[0].bookName);
        }

        [Test]
        public void ParseCatalog_CsvExtension_RoutesToParseCSV()
        {
            List<PRBook> books = Globals.ParseCatalog("http://cdn/stories_02/stories.csv", CatalogCsv);

            Assert.AreEqual(3, books.Count);
            Assert.AreEqual("Alphabet Rhymebook", books[0].bookName);
            Assert.AreEqual("rhymebooks : family : special education", books[0].genre);
        }

        [Test]
        public void ParseCatalog_JsonExtension_IsCaseInsensitive()
        {
            List<PRBook> books = Globals.ParseCatalog("http://cdn/stories.JSON", CatalogJson);

            Assert.AreEqual(3, books.Count);
        }

        // --- Learn-to-read ladder fields: level / phonics_focus mapping (v1 feature) ---

        [Test]
        public void Level_WhenPresent_IsMapped()
        {
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""The Sun Is Up"", ""author"": ""A"", ""cover"": ""c.jpg"", ""script"": ""good/script.txt"",
                ""age_from"": 3, ""age_to"": 6, ""genres"": [ ""learn to read"" ], ""notes_for_parents"": ""n"", ""id"": ""1"",
                ""level"": 2, ""phonics_focus"": ""the oa sound"" } ] }";

            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual(2, books[0].level);
            Assert.AreEqual("the oa sound", books[0].phonicsFocus);
        }

        [Test]
        public void Level_WhenAbsent_DefaultsToZeroAndEmptyPhonics()
        {
            // The realistic fixture's three books carry no level/phonics_focus — they must
            // default exactly like the CSV path (0 / "").
            List<PRBook> books = Globals.ParseJSON(CatalogJson);

            foreach (PRBook b in books)
            {
                Assert.AreEqual(0, b.level, $"level should default to 0 for {b.bookName}");
                Assert.AreEqual("", b.phonicsFocus, $"phonicsFocus should default to \"\" for {b.bookName}");
            }
        }

        [Test]
        public void Csv_AndJson_BothDefaultLevelAndPhonics_ForBooksWithoutThem()
        {
            // Equivalence-partner books (no level data on either path) stay identical:
            // CSV never carries these columns, JSON omits them here → both default.
            List<PRBook> fromJson = Globals.ParseJSON(CatalogJson);
            List<PRBook> fromCsv = Globals.ParseCSV(CatalogCsv);

            for (int i = 0; i < fromJson.Count; i++)
            {
                Assert.AreEqual(fromCsv[i].level, fromJson[i].level, $"level at {i}");
                Assert.AreEqual(fromCsv[i].phonicsFocus, fromJson[i].phonicsFocus, $"phonicsFocus at {i}");
                Assert.AreEqual(0, fromCsv[i].level, $"csv level at {i}");
                Assert.AreEqual("", fromCsv[i].phonicsFocus, $"csv phonicsFocus at {i}");
            }
        }

        // --- Navigation-tile field: action mapping (nav-tiles feature) ---

        [Test]
        public void Action_WhenPresent_IsMapped()
        {
            // A nav tile carries an action (a Nav address) and no script.
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""Level 1"", ""cover"": ""nav/level1.jpg"", ""id"": ""nav_level1"",
                ""action"": ""library?filter=level1"" } ] }";

            List<PRBook> books = Globals.ParseJSON(json);

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("library?filter=level1", books[0].action);
        }

        [Test]
        public void Action_WhenAbsent_DefaultsToEmptyString()
        {
            // Realistic fixture's books are normal books — no action → "" (a normal book).
            List<PRBook> books = Globals.ParseJSON(CatalogJson);

            foreach (PRBook b in books)
                Assert.AreEqual("", b.action, $"action should default to \"\" for {b.bookName}");
        }
    }
}
