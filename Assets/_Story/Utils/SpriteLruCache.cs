using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Least-recently-used cache of Sprites bounded by an approximate GPU BYTE
/// budget instead of an entry count.
///
/// A count cap only works when every entry is roughly the same size. It is not:
/// a library cover is ~150 KB decoded while a 2700x2700 story page is ~28 MB, so
/// a 100-entry cap permits anything from 15 MB to 2.8 GB. Bytes are the thing
/// the device actually runs out of, so bytes are what we cap.
///
/// <paramref name="protectNewest"/> entries are never evicted, however far over
/// budget we are. The current page's sprites and the prefetched next page's are
/// always the most recently added, so this keeps a Sprite from being destroyed
/// while an Image is still drawing it.
///
/// Pure C#: it never touches the Unity object lifetime itself. Freeing the
/// memory is the owner's job via <see cref="OnEvict"/>.
/// </summary>
public class SpriteLruCache
{
    private class Entry
    {
        public string Key;
        public Sprite Sprite;
        public long Bytes;
    }

    private readonly long budgetBytes;
    private readonly int protectNewest;
    private readonly Dictionary<string, LinkedListNode<Entry>> map =
        new Dictionary<string, LinkedListNode<Entry>>();
    // First node = least recently used, last node = most recently used.
    private readonly LinkedList<Entry> order = new LinkedList<Entry>();

    /// <summary>Invoked exactly once per entry dropped by eviction, with that
    /// entry's Sprite. Set by the owner to release the underlying objects.</summary>
    public Action<Sprite> OnEvict;

    /// <summary>Optional: the set of Sprites something is still displaying. Called ONCE at
    /// the start of an eviction pass (never when we are inside budget); entries whose Sprite
    /// is in the set are skipped instead of destroyed, however old they are. Null = nothing
    /// is known to be in use, which is the pure-C# behaviour the tests exercise.</summary>
    public Func<HashSet<Sprite>> inUseProvider;

    /// <summary>Approximate decoded bytes currently held.</summary>
    public long Bytes { get; private set; }

    public int Count { get { return map.Count; } }

    public SpriteLruCache(long budgetBytes, int protectNewest)
    {
        this.budgetBytes = budgetBytes;
        this.protectNewest = protectNewest;
    }

    /// <summary>Approximate decoded size of a Sprite's texture, as RGBA32 with
    /// no mips. 0 for a null Sprite or a Sprite with no texture.</summary>
    public static long SizeOf(Sprite sprite)
    {
        if (sprite == null) return 0;
        Texture2D tex = sprite.texture;
        if (tex == null) return 0;
        return (long)tex.width * tex.height * 4;
    }

    /// <summary>Look up a key. On a hit the entry becomes most-recently-used.</summary>
    public bool TryGet(string key, out Sprite sprite)
    {
        LinkedListNode<Entry> node;
        if (key != null && map.TryGetValue(key, out node))
        {
            order.Remove(node);
            order.AddLast(node);
            sprite = node.Value.Sprite;
            return true;
        }
        sprite = null;
        return false;
    }

    /// <summary>Insert or replace a key as most-recently-used, then evict
    /// least-recently-used entries until we are back within budget.</summary>
    public void Add(string key, Sprite sprite)
    {
        if (key == null) return;

        LinkedListNode<Entry> existing;
        if (map.TryGetValue(key, out existing))
        {
            // Replace in place: drop the old entry's bytes so a re-add of the
            // same url can't double-count. The old Sprite is not evicted — the
            // caller owns it and may still be displaying it.
            Bytes -= existing.Value.Bytes;
            order.Remove(existing);
            map.Remove(key);
        }

        var entry = new Entry { Key = key, Sprite = sprite, Bytes = SizeOf(sprite) };
        map[key] = order.AddLast(entry);
        Bytes += entry.Bytes;

        // Evict now, unless the owner asked to defer: a freshly loaded Sprite is in the cache
        // for a moment before its Image assigns it, so an inline pass with several loads in
        // flight can destroy a cover that is about to be displayed. With deferEviction set, the
        // owner calls Trim() at the end of the frame, when every assignment of that frame is done.
        if (deferEviction) { trimPending = true; return; }
        EvictToBudget();
    }

    /// <summary>See <see cref="deferEviction"/>.</summary>
    public bool deferEviction;
    private bool trimPending;

    /// <summary>Run the deferred eviction pass (no-op when nothing was added since the last one).</summary>
    public void Trim()
    {
        if (!trimPending) return;
        trimPending = false;
        EvictToBudget();
    }

    private void EvictToBudget()
    {
        // Nothing to do inside budget — and in particular, do not pay for the in-use scan.
        if (Bytes <= budgetBytes || Count <= protectNewest) return;

        // One snapshot for the whole pass: the scan is the expensive part, and nothing can
        // start or stop displaying a Sprite in the middle of this loop.
        HashSet<Sprite> inUse = inUseProvider != null ? inUseProvider() : null;

        // Walk oldest-first, considering each entry present when the pass began exactly once.
        // The `remaining` counter is what makes that "exactly once" true: skipped entries go to
        // the tail, so following node.Next alone would walk in circles forever once two or more
        // entries are in use.
        int remaining = Count;
        LinkedListNode<Entry> node = order.First;
        while (node != null && remaining-- > 0 && Bytes > budgetBytes && Count > protectNewest)
        {
            LinkedListNode<Entry> next = node.Next;

            if (inUse != null && node.Value.Sprite != null && inUse.Contains(node.Value.Sprite))
            {
                // Still on screen. Destroying it would leave a grey square with nothing to
                // reload it, so keep it and carry on with the next LRU entry — even if that
                // means finishing the pass over budget.
                order.Remove(node);
                order.AddLast(node);
            }
            else
            {
                order.Remove(node);
                map.Remove(node.Value.Key);
                Bytes -= node.Value.Bytes;

                if (OnEvict != null) OnEvict(node.Value.Sprite);
            }

            node = next;
        }
    }
}
