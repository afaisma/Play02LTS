using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for SpriteLruCache — the byte-budgeted LRU that bounds PRUtils.cacheImages.
// Sprites are backed by real 2x2 RGBA textures (16 bytes each by SizeOf), so budgets here are
// expressed in multiples of 16. Every texture created is destroyed in [TearDown].
namespace ReadingBuddy.Tests
{
    public class SpriteLruCacheTests
    {
        private const long SpriteBytes = 2 * 2 * 4; // what SizeOf reports for a 2x2 sprite
        private readonly List<Object> spawned = new List<Object>();

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            spawned.Add(tex);
            spawned.Add(sprite);
            return sprite;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned)
                if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
        }

        [Test]
        public void SizeOf_CountsTextureAsRGBA32()
        {
            Assert.AreEqual(SpriteBytes, SpriteLruCache.SizeOf(MakeSprite()));
        }

        [Test]
        public void SizeOf_NullSpriteIsZero()
        {
            Assert.AreEqual(0, SpriteLruCache.SizeOf(null));
        }

        [Test]
        public void UnderBudget_NothingIsEvicted()
        {
            var cache = new SpriteLruCache(SpriteBytes * 3, 0);
            cache.Add("a", MakeSprite());
            cache.Add("b", MakeSprite());
            cache.Add("c", MakeSprite());

            Assert.AreEqual(3, cache.Count);
            Assert.AreEqual(SpriteBytes * 3, cache.Bytes);
        }

        [Test]
        public void OverBudget_EvictsOldestFirst()
        {
            var cache = new SpriteLruCache(SpriteBytes * 2, 0);
            cache.Add("a", MakeSprite());
            cache.Add("b", MakeSprite());
            cache.Add("c", MakeSprite());

            Sprite ignored;
            Assert.IsFalse(cache.TryGet("a", out ignored), "oldest entry should have been evicted");
            Assert.IsTrue(cache.TryGet("b", out ignored));
            Assert.IsTrue(cache.TryGet("c", out ignored));
            Assert.AreEqual(2, cache.Count);
            Assert.AreEqual(SpriteBytes * 2, cache.Bytes);
        }

        [Test]
        public void ProtectNewest_EntriesSurviveEvenWhenOverBudget()
        {
            // Budget of one sprite, but the two newest are protected.
            var cache = new SpriteLruCache(SpriteBytes, 2);
            cache.Add("a", MakeSprite());
            cache.Add("b", MakeSprite());
            cache.Add("c", MakeSprite());

            Sprite ignored;
            Assert.IsFalse(cache.TryGet("a", out ignored));
            Assert.IsTrue(cache.TryGet("b", out ignored), "protected entry was evicted");
            Assert.IsTrue(cache.TryGet("c", out ignored), "protected entry was evicted");
            Assert.AreEqual(2, cache.Count);
            Assert.AreEqual(SpriteBytes * 2, cache.Bytes, "cache stays over budget rather than evict a protected entry");
        }

        [Test]
        public void TryGet_RefreshesRecency()
        {
            var cache = new SpriteLruCache(SpriteBytes * 2, 0);
            cache.Add("a", MakeSprite());
            cache.Add("b", MakeSprite());

            Sprite ignored;
            cache.TryGet("a", out ignored);   // "a" is now the most recently used
            cache.Add("c", MakeSprite());     // pushes one entry out

            Assert.IsTrue(cache.TryGet("a", out ignored), "touched entry should not be the next evicted");
            Assert.IsFalse(cache.TryGet("b", out ignored), "untouched entry should have been evicted");
        }

        [Test]
        public void OnEvict_IsCalledExactlyOncePerEvictedEntry()
        {
            var cache = new SpriteLruCache(SpriteBytes * 2, 0);
            var evicted = new List<Sprite>();
            cache.OnEvict = s => evicted.Add(s);

            Sprite a = MakeSprite();
            Sprite b = MakeSprite();
            cache.Add("a", a);
            cache.Add("b", b);
            cache.Add("c", MakeSprite());
            cache.Add("d", MakeSprite());

            Assert.AreEqual(2, evicted.Count);
            Assert.AreSame(a, evicted[0]);
            Assert.AreSame(b, evicted[1]);
        }

        [Test]
        public void OnEvict_NotCalledForAReplacedKey()
        {
            var cache = new SpriteLruCache(SpriteBytes * 2, 0);
            int evictions = 0;
            cache.OnEvict = s => evictions++;

            cache.Add("a", MakeSprite());
            cache.Add("a", MakeSprite());

            Assert.AreEqual(0, evictions, "replacing a key is not an eviction — the caller still owns the old sprite");
        }

        [Test]
        public void AddExistingKey_ReplacesWithoutDoubleCountingBytes()
        {
            var cache = new SpriteLruCache(SpriteBytes * 4, 0);
            cache.Add("a", MakeSprite());
            Sprite replacement = MakeSprite();
            cache.Add("a", replacement);

            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(SpriteBytes, cache.Bytes);

            Sprite got;
            Assert.IsTrue(cache.TryGet("a", out got));
            Assert.AreSame(replacement, got);
        }

        [Test]
        public void AddExistingKey_MakesItMostRecentlyUsed()
        {
            var cache = new SpriteLruCache(SpriteBytes * 2, 0);
            cache.Add("a", MakeSprite());
            cache.Add("b", MakeSprite());
            cache.Add("a", MakeSprite());   // re-add refreshes recency
            cache.Add("c", MakeSprite());

            Sprite ignored;
            Assert.IsTrue(cache.TryGet("a", out ignored));
            Assert.IsFalse(cache.TryGet("b", out ignored));
        }

        [Test]
        public void TryGet_MissReportsNullAndFalse()
        {
            var cache = new SpriteLruCache(SpriteBytes, 0);
            Sprite got;
            Assert.IsFalse(cache.TryGet("nope", out got));
            Assert.IsNull(got);
        }
    }
}
