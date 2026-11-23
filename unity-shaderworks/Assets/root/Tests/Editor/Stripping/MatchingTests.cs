using System.Collections.Generic;
using NUnit.Framework;

namespace Saesentsessis.Shaders.Stripping.Tests
{
	/// <summary>
	/// Decoding variants per pass and the keep / exclude rules, on a schema shaped like a two-pass shader whose first pass
	/// has a group the second lacks.
	/// </summary>
	public class MatchingTests
	{
		private static readonly KeywordGroup Fade = new("fade", "Fade", new[] { KeywordValue.None("Off"), KeywordValue.Of("_FADE", "On") });
		private static readonly KeywordGroup Phase = new("phase", "Phase", new[] { KeywordValue.None("Iso"), KeywordValue.Of("_HG", "HG") });
		private static readonly KeywordGroup Mode = new("mode", "Mode", new[] { KeywordValue.Of("_A", "A"), KeywordValue.Of("_B", "B") });
		private static readonly ShaderKeywordSchema Schema = new("test", "Hidden/Test", "Test", new[] { Fade, Phase, Mode });

		private static PassLayout Pass0 => PassLayout.Create(Schema, new HashSet<string> { "_FADE", "_HG", "_A", "_B" });
		private static PassLayout Pass1 => PassLayout.Create(Schema, new HashSet<string> { "_HG", "_A", "_B" });

		private static VariantBits Decode(PassLayout layout, params string[] enabled)
		{
			Assert.IsTrue(layout.TryDecode(new KeywordNameQuery(new HashSet<string>(enabled), layout.SlotKeywords), out var bits));
			return bits;
		}

		private static ExclusionRule Exclusion(params (KeywordGroup group, int value)[] values)
		{
			var combination = new ExcludedCombination(Schema.Id);
			foreach (var (group, value) in values)
				combination.SetValue(Schema, Schema.IndexOf(group), value);

			var groups = new List<int>();
			Assert.IsTrue(combination.TryGetPattern(Schema, out var pattern, groups));
			return new ExclusionRule(pattern, groups.ToArray());
		}

		[Test]
		public void Decode_SetsOneValuePerDeclaredGroup()
		{
			var bits = Decode(Pass0, "_HG", "_B");
			Assert.AreEqual(1UL << 0, Schema.GetValues(bits, 0), "Fade off: its value without a keyword.");
			Assert.AreEqual(1UL << 1, Schema.GetValues(bits, 1));
			Assert.AreEqual(1UL << 1, Schema.GetValues(bits, 2));

			var pass1 = Decode(Pass1, "_A");
			Assert.AreEqual(0UL, Schema.GetValues(pass1, 0), "A group the pass doesn't declare has no bits.");
		}

		[Test]
		public void Decode_RejectsVariantsThatDontFitTheSchema()
		{
			var layout = Pass0;
			Assert.IsFalse(layout.TryDecode(new KeywordNameQuery(new HashSet<string> { "_A", "_B" }, layout.SlotKeywords), out _),
				"Two values of one group.");
			Assert.IsFalse(layout.TryDecode(new KeywordNameQuery(new HashSet<string>(), layout.SlotKeywords), out _),
				"No value of a group without a no-keyword value.");
		}

		[Test]
		public void Keep_AcceptsEveryVariantInsideAPattern()
		{
			var pattern = Schema.WithValue(Schema.WithValue(Schema.AllValues, Fade, 1), Phase, 0);
			var keep = new[] { pattern };
			var none = new VariantBits[0];

			Assert.IsTrue(VariantMatching.IsKept(Decode(Pass0, "_FADE", "_A"), keep, none));
			Assert.IsFalse(VariantMatching.IsKept(Decode(Pass0, "_A"), keep, none), "Fade off isn't kept.");
			Assert.IsFalse(VariantMatching.IsKept(Decode(Pass0, "_FADE", "_HG", "_A"), keep, none), "HG isn't kept.");
			Assert.IsTrue(VariantMatching.IsKept(Decode(Pass1, "_A"), keep, none), "Pass 1 has no fade group: kept.");
		}

		[Test]
		public void Exclusions_OnlyApplyWhereEveryListedGroupIsDeclared()
		{
			var keep = new[] { Schema.AllValues };

			// "Fade off + HG": meant for pass 0; pass 1 has no fade group, so its HG variants stay.
			var rules = new[] { Exclusion((Fade, 0), (Phase, 1)) };
			var pass0 = Pass0;
			var pass1 = Pass1;
			pass0.ApplyExclusions(rules);
			pass1.ApplyExclusions(rules);

			Assert.IsFalse(VariantMatching.IsKept(Decode(pass0, "_HG", "_A"), keep, pass0.Exclusions));
			Assert.IsTrue(VariantMatching.IsKept(Decode(pass0, "_FADE", "_HG", "_A"), keep, pass0.Exclusions));
			Assert.IsTrue(VariantMatching.IsKept(Decode(pass1, "_HG", "_A"), keep, pass1.Exclusions));

			// "HG + B", any fade: applies to both passes.
			rules = new[] { Exclusion((Phase, 1), (Mode, 1)) };
			pass1.ApplyExclusions(rules);
			Assert.IsFalse(VariantMatching.IsKept(Decode(pass1, "_HG", "_B"), keep, pass1.Exclusions));
			Assert.IsTrue(VariantMatching.IsKept(Decode(pass1, "_HG", "_A"), keep, pass1.Exclusions));
		}

		[Test]
		public void Exclusion_NamingARemovedValue_StripsNothing()
		{
			var combination = new ExcludedCombination(Schema.Id);
			combination.Values.Add(new GroupValue("phase", "_REMOVED"));
			Assert.IsFalse(combination.TryGetPattern(Schema, out _, new List<int>()));

			Assert.IsFalse(new ExcludedCombination(Schema.Id).TryGetPattern(Schema, out _, new List<int>()),
				"An entry with Any everywhere would strip everything: it strips nothing.");
		}
	}
}
