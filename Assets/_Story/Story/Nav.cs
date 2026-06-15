using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// String-addressable navigation router. One consistent way to navigate to a
/// scene *with state* from any scene's button, code, or a story script.
///
/// Address grammar: <c>"&lt;scene&gt;[?key=value&amp;key=value]"</c> — scene is
/// case-insensitive; <c>'+'</c> and <c>%20</c> in values decode to a space.
/// Examples: <c>"library?filter=level1"</c>, <c>"story?book=The Big Pig"</c>.
///
/// This layer DELEGATES to the existing entry points (Globals.GotoLibrary,
/// Globals.GotoPrBook, Navigation.GoToX) — it does not replace them. Fail soft:
/// an unknown address or an unresolvable book is a logged no-op, never a crash
/// and never a scene load with null state.
/// </summary>
public static class Nav
{
    // Dispatch a string address to the matching navigation, delegating to the
    // existing methods. Unknown address / unresolvable book → logged no-op.
    public static void Go(string address)
    {
        var (scene, args) = Parse(address);
        switch (scene)
        {
            case "library":
                GoToLibrary(args.GetValueOrDefault("filter", "everything"));
                break;
            case "story":
            {
                // The `page` arg (if present) is parsed and accepted but ignored
                // this phase — the Story start-page hook is out of scope.
                PRBook book = ResolveBook(args);
                if (book == null)
                {
                    Debug.LogWarning($"Nav: could not resolve book for address \"{address}\" (catalog not loaded or no match); no-op.");
                    return;
                }
                GoToBook(book);
                break;
            }
            case "bookstore":
                Globals.g_bookstoreFilter = args.GetValueOrDefault("filter", "everything");
                Navigation.GoToBookstore();
                break;
            case "settings":
                Navigation.GoToSettings();
                break;
            case "parents":
                Navigation.GoToParents();
                break;
            case "map":
                Navigation.GoToMap();
                break;
            case "message":
                Navigation.GoToMessage();
                break;
            case "start":
                Navigation.GoToStart();
                break;
            default:
                Debug.LogWarning($"Nav: unknown address \"{address}\" (scene \"{scene}\"); no-op.");
                break;
        }
    }

    // Thin, compile-time-safe wrappers — what most app code should call.
    public static void GoToLibrary(string filter = "everything") => Globals.GotoLibrary(filter);

    public static void GoToBook(PRBook book) => Globals.GotoPrBook(book);

    public static void GoToBookByName(string name)
    {
        var book = ResolveBook(new Dictionary<string, string> { ["book"] = name });
        if (book == null)
        {
            Debug.LogWarning($"Nav: GoToBookByName(\"{name}\") found no matching book; no-op.");
            return;
        }
        GoToBook(book);
    }

    public static void GoToBookById(string id)
    {
        var book = ResolveBook(new Dictionary<string, string> { ["id"] = id });
        if (book == null)
        {
            Debug.LogWarning($"Nav: GoToBookById(\"{id}\") found no matching book; no-op.");
            return;
        }
        GoToBook(book);
    }

    // Parse "<scene>[?key=value&key=value]" into (scene, args). Scene is
    // lower-cased + trimmed. Values decode '+' and %20 to spaces (no full
    // URL-decode needed). Empty/blank address → scene "" + empty args.
    public static (string scene, Dictionary<string, string> args) Parse(string address)
    {
        var args = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(address))
            return ("", args);

        int q = address.IndexOf('?');
        string scene = (q < 0 ? address : address.Substring(0, q)).Trim().ToLowerInvariant();

        if (q >= 0 && q < address.Length - 1)
        {
            string query = address.Substring(q + 1);
            foreach (string pair in query.Split('&'))
            {
                if (pair.Length == 0)
                    continue;
                int eq = pair.IndexOf('=');
                string key = (eq < 0 ? pair : pair.Substring(0, eq)).Trim();
                string value = eq < 0 ? "" : pair.Substring(eq + 1);
                value = value.Replace('+', ' ').Replace("%20", " ");
                if (key.Length > 0)
                    args[key] = value;
            }
        }

        return (scene, args);
    }

    // Resolve a book from address args against the loaded catalog
    // (Globals.g_listPRBooks). Returns null if the catalog is not loaded or no
    // match — by id, then book name (case-insensitive), then bookUrl.
    public static PRBook ResolveBook(Dictionary<string, string> args)
    {
        if (args == null || Globals.g_listPRBooks == null)
            return null;

        if (args.TryGetValue("id", out string id) && !string.IsNullOrEmpty(id))
            return Globals.g_listPRBooks.Find(b => b.id == id);

        if (args.TryGetValue("book", out string name) && !string.IsNullOrEmpty(name))
            return Globals.g_listPRBooks.Find(b => string.Equals(b.bookName, name, System.StringComparison.OrdinalIgnoreCase));

        if (args.TryGetValue("url", out string url) && !string.IsNullOrEmpty(url))
            return Globals.g_listPRBooks.Find(b => b.bookUrl == url);

        return null;
    }
}
