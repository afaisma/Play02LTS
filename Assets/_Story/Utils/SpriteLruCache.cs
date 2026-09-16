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

        EvictToBudget();
    }

    private void EvictToBudget()
    {
        while (Bytes > budgetBytes && Count > protectNewest)
        {
            LinkedListNode<Entry> lru = order.First;
            order.RemoveFirst();
            map.Remove(lru.Value.Key);
            Bytes -= lru.Value.Bytes;

            if (OnEvict != null) OnEvict(lru.Value.Sprite);
        }
    }
}
