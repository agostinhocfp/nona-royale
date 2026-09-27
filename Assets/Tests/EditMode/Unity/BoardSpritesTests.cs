// Assets/Tests/EditMode/Unity/BoardSpritesTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// Holds <see cref="BoardSprites"/> to the contract in
    /// HANDOFF_board_assets.md §1 (board skin BS1).
    /// </summary>
    [TestFixture]
    public class BoardSpritesTests
    {
        /// <summary>
        /// The contract table, typed out. A slot added or renamed on either side
        /// fails here until the other side matches.
        /// </summary>
        private static readonly (string name, int size, bool tinted)[] Contract =
        {
            ("board_cell_track", 256, false),
            ("board_cell_home", 256, true),
            ("board_start_emblem", 256, true),
            ("board_medallion", 512, false),
            ("board_medallion_emblem", 512, false),
            ("board_corner_wedge", 256, false),
            ("yard_table_felt", 1024, true),
            ("yard_table_rim", 1024, false),
            ("yard_table_emblem", 256, false),
            ("yard_chair", 256, true),
            ("prop_candle", 256, false),
            ("prop_chips", 256, false),
            ("prop_plant", 512, false),
            ("prop_instrument", 512, false),
        };

        [SetUp]
        public void SetUp() => BoardSprites.ClearCache();

        [TearDown]
        public void TearDown() => BoardSprites.ClearCache();

        [Test]
        public void Slots_MatchTheContract_NameSizeAndTint()
        {
            var actual = BoardSprites.Slots.Select(s => (s.Name, s.SizePx, s.Tinted)).ToArray();
            CollectionAssert.AreEqual(Contract, actual);
        }

        [Test]
        public void EverySlot_HasAPublicConstant_AndEveryConstantIsASlot()
        {
            var constants = typeof(BoardSprites)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(BoardSprites.Folder))
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();

            CollectionAssert.AreEquivalent(BoardSprites.Slots.Select(s => s.Name).ToList(), constants);
        }

        [Test]
        public void SlotNames_AreUnique()
        {
            var names = BoardSprites.Slots.Select(s => s.Name).ToList();
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void SlotNames_NeverClaimTheThreeTextures()
        {
            foreach (var texture in new[] { BoardTextures.MarbleName, BoardTextures.FeltName, BoardTextures.CarpetName })
                Assert.IsFalse(BoardSprites.IsSlot(texture), texture);
        }

        [Test]
        public void SlotNames_ArePlainLowercaseWithAKnownPrefix()
        {
            foreach (var slot in BoardSprites.Slots)
            {
                StringAssert.IsMatch("^(board|yard|prop)_[a-z]+(_[a-z]+)*$", slot.Name);
            }
        }

        [Test]
        public void Sizes_ArePowersOfTwo()
        {
            foreach (var slot in BoardSprites.Slots)
                Assert.That(slot.SizePx >= 64 && (slot.SizePx & (slot.SizePx - 1)) == 0, $"{slot.Name}: {slot.SizePx}");
        }

        [Test]
        public void TryGetSlot_FindsEverySlot_AndNothingElse()
        {
            foreach (var slot in BoardSprites.Slots)
            {
                Assert.IsTrue(BoardSprites.TryGetSlot(slot.Name, out var found), slot.Name);
                Assert.AreEqual(slot.SizePx, found.SizePx);
            }

            Assert.IsFalse(BoardSprites.TryGetSlot("board_cell", out _));
            Assert.IsFalse(BoardSprites.TryGetSlot("Board_Cell_Track", out _));
            Assert.IsFalse(BoardSprites.TryGetSlot(null, out _));
        }

        [Test]
        public void Get_OfAnUnknownName_Throws()
        {
            Assert.Throws<ArgumentException>(() => BoardSprites.Get("board_celltrack"));
            Assert.Throws<ArgumentException>(() => BoardSprites.Register("nope", null));
        }

        [Test]
        public void Registered_Sprite_IsReturned_AndClearCacheForgetsIt()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            try
            {
                BoardSprites.Register(BoardSprites.Chair, sprite);
                Assert.AreSame(sprite, BoardSprites.Get(BoardSprites.Chair));
                Assert.IsTrue(BoardSprites.Has(BoardSprites.Chair));
                CollectionAssert.Contains(BoardSprites.Filled().ToList(), BoardSprites.Chair);

                BoardSprites.Register(BoardSprites.Chair, null);
                Assert.IsFalse(BoardSprites.Has(BoardSprites.Chair));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void UnitScale_DrawsASpriteOneUnitAcross_WhateverItsPixelsPerUnit()
        {
            var texture = new Texture2D(64, 64);
            var sprites = new List<Sprite>
            {
                Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f),
                Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f),
                Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 16f),
            };
            try
            {
                foreach (var sprite in sprites)
                    Assert.AreEqual(1f, sprite.bounds.size.x * BoardSprites.UnitScale(sprite), 1e-4f);

                Assert.AreEqual(1f, BoardSprites.UnitScale(null));
            }
            finally
            {
                foreach (var sprite in sprites) UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
