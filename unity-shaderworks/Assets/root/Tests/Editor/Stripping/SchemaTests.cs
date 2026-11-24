using System;
using NUnit.Framework;

namespace Saesentsessis.Shaders.Stripping.Tests
{
	public class SchemaTests
	{
		internal static KeywordGroup Group(string id, int keywordCount, bool withNone = true)
		{
			var values = new KeywordValue[keywordCount + (withNone ? 1 : 0)];
			var i = 0;
			if (withNone)
				values[i++] = KeywordValue.None("Off");

			for (var k = 0; k < keywordCount; k++)
				values[i++] = KeywordValue.Of($"_{id.ToUpperInvariant()}_{k}", $"{id} {k}");

			return new KeywordGroup(id, id, values);
		}

		[Test]
		public void VariantBits_SetGetAndRanges_WorkAcrossWords()
		{
			var bits = default(VariantBits);
			foreach (var bit in new[] { 0, 63, 64, 130, 255 })
				bits.Set(bit);

			Assert.IsTrue(bits.Get(63) && bits.Get(64) && bits.Get(130) && bits.Get(255));
			Assert.IsFalse(bits.Get(1));

			bits.SetRange(128, 5, 0b10101UL);
			Assert.AreEqual(0b10101UL, bits.GetRange(128, 5));
			Assert.IsTrue(bits.Get(130), "Bit 130 is part of the new range (value bit 2 is set).");

			Assert.IsTrue(VariantBits.FirstBits(3).IsSubsetOf(VariantBits.FirstBits(5)));
			Assert.IsFalse(VariantBits.FirstBits(5).IsSubsetOf(VariantBits.FirstBits(3)));
			Assert.AreEqual(VariantBits.FirstBits(256), ~VariantBits.Empty);
		}

		[Test]
		public void Layout_KeepsEveryGroupInsideOneWord()
		{
			var a = Group("a", 39); // 40 values
			var b = Group("b", 39); // would cross bit 64
			var schema = new ShaderKeywordSchema("s", "Hidden/Test", "Test", new[] { a, b });

			Assert.AreEqual(0, schema.OffsetOf(0));
			Assert.AreEqual(64, schema.OffsetOf(1), "The second group starts the next word.");
			Assert.AreEqual(104, schema.BitCount);
			Assert.AreEqual(a.AllValues, schema.GetValues(schema.AllValues, 0));
			Assert.AreEqual(b.AllValues, schema.GetValues(schema.AllValues, 1));
		}

		[Test]
		public void InvalidSchemas_AreRejected()
		{
			Assert.Throws<ArgumentException>(() => new KeywordGroup("g", "g", new[] { KeywordValue.None("a"), KeywordValue.None("b") }));
			Assert.Throws<ArgumentException>(() => new KeywordGroup("g", "g", new[] { KeywordValue.Of("_A", "a"), KeywordValue.Of("_A", "b") }));
			Assert.Throws<ArgumentException>(() => new KeywordGroup("g", "g", new KeywordValue[65]));
			Assert.Throws<ArgumentException>(() => KeywordValue.Of("_", "none"));

			var shared = new KeywordGroup("x", "x", new[] { KeywordValue.None("off"), KeywordValue.Of("_SAME", "on") });
			var other = new KeywordGroup("y", "y", new[] { KeywordValue.None("off"), KeywordValue.Of("_SAME", "on") });
			Assert.Throws<ArgumentException>(() => new ShaderKeywordSchema("s", "Hidden/Test", "Test", new[] { shared, other }));

			var big = new KeywordGroup[5];
			for (var i = 0; i < big.Length; i++)
				big[i] = Group("g" + i, 63);
			Assert.Throws<ArgumentException>(() => new ShaderKeywordSchema("s", "Hidden/Test", "Test", big), "320 bits don't fit.");
		}

		[Test]
		public void FromKeywords_FillsUnlistedGroupsWithTheirNoKeywordValue()
		{
			var a = Group("a", 2);
			var b = Group("b", 2);
			var c = Group("c", 2, withNone: false);
			var schema = new ShaderKeywordSchema("s", "Hidden/Test", "Test", new[] { a, b, c });

			var bits = schema.FromKeywords(new[] { "_A_1", "_UNRELATED" });

			Assert.AreEqual(1UL << 2, schema.GetValues(bits, 0), "Listed keyword.");
			Assert.AreEqual(1UL << 0, schema.GetValues(bits, 1), "Not listed: the value without a keyword.");
			Assert.AreEqual(c.AllValues, schema.GetValues(bits, 2), "Not listed and no such value: any value.");
		}

		[Test]
		public void PatternHelpers_LimitOneGroupAndKeepTheOthers()
		{
			var a = Group("a", 2);
			var b = Group("b", 3);
			var schema = new ShaderKeywordSchema("s", "Hidden/Test", "Test", new[] { a, b });

			var pattern = schema.WithKeyword(schema.AllValues, b, "_B_2");
			Assert.AreEqual(1UL << 3, schema.GetValues(pattern, 1));
			Assert.AreEqual(a.AllValues, schema.GetValues(pattern, 0));

			pattern = schema.WithKeyword(pattern, a, null);
			Assert.AreEqual(1UL, schema.GetValues(pattern, 0));
			Assert.Throws<ArgumentException>(() => schema.WithKeyword(pattern, a, "_MISSING"));
		}
	}
}
