using System;
using System.Collections.Generic;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// The schema as one pass and stage sees it: which keyword groups it declares, and how to turn a variant's keywords
	/// into <see cref="VariantBits"/>. Groups it doesn't declare contribute no bits, so every keep pattern accepts them,
	/// and exclusions naming a value of them don't apply to it.
	/// </summary>
	internal sealed class PassLayout
	{
		/// <param name="declaredKeywords">The keywords the pass and stage declare.</param>
		public static PassLayout Create(ShaderKeywordSchema schema, ICollection<string> declaredKeywords)
		{
			var present = new bool[schema.Groups.Count];
			var groupStarts = new int[schema.Groups.Count + 1];
			var slotKeywords = new List<string>();
			var slotBits = new List<int>();

			for (var g = 0; g < schema.Groups.Count; g++)
			{
				groupStarts[g] = slotKeywords.Count;
				var group = schema.Groups[g];
				for (var v = 0; v < group.Count; v++)
				{
					var keyword = group.Values[v].Keyword;
					if (keyword == null || declaredKeywords.Contains(keyword) == false)
						continue;

					present[g] = true;
					slotKeywords.Add(keyword);
					slotBits.Add(schema.BitOf(g, v));
				}
			}

			groupStarts[schema.Groups.Count] = slotKeywords.Count;
			return new PassLayout(schema, present, groupStarts, slotKeywords.ToArray(), slotBits.ToArray());
		}

		private readonly ShaderKeywordSchema _schema;
		private readonly bool[] _present;
		private readonly int[] _groupStarts;
		private readonly string[] _slotKeywords;
		private readonly int[] _slotBits;

		private PassLayout(ShaderKeywordSchema schema, bool[] present, int[] groupStarts, string[] slotKeywords, int[] slotBits)
		{
			_schema = schema;
			_present = present;
			_groupStarts = groupStarts;
			_slotKeywords = slotKeywords;
			_slotBits = slotBits;
			Exclusions = Array.Empty<VariantBits>();
		}

		/// <summary>
		/// The keyword of each slot, in slot order (what an <see cref="IKeywordQuery"/> answers about).
		/// </summary>
		public IReadOnlyList<string> SlotKeywords => _slotKeywords;

		/// <summary>
		/// The exclusions that apply to this pass and stage (set by <see cref="ApplyExclusions"/>).
		/// </summary>
		public VariantBits[] Exclusions { get; private set; }

		public bool IsPresent(int groupIndex) => _present[groupIndex];

		/// <summary>
		/// Keeps the exclusions whose every listed group this pass and stage declare.
		/// </summary>
		public void ApplyExclusions(IReadOnlyList<ExclusionRule> exclusions)
		{
			var applicable = new List<VariantBits>(exclusions.Count);
			foreach (var exclusion in exclusions)
			{
				var applies = true;
				foreach (var group in exclusion.Groups)
					applies &= _present[group];

				if (applies)
					applicable.Add(exclusion.Pattern);
			}

			Exclusions = applicable.ToArray();
		}

		/// <summary>
		/// Turns a variant's keywords into bits: per declared group the one value that is on, or its value without a
		/// keyword when none is.
		/// </summary>
		/// <returns>False when the variant doesn't fit the schema (two values of a group on, or none on in a group without
		/// a no-keyword value): such a variant is never stripped.</returns>
		public bool TryDecode<TQuery>(in TQuery query, out VariantBits bits) where TQuery : struct, IKeywordQuery
		{
			bits = default;
			for (var g = 0; g < _present.Length; g++)
			{
				if (_present[g] == false)
					continue;

				var bit = -1;
				for (var slot = _groupStarts[g]; slot < _groupStarts[g + 1]; slot++)
				{
					if (query.IsEnabled(slot) == false)
						continue;

					if (bit >= 0)
						return false;

					bit = _slotBits[slot];
				}

				if (bit < 0)
				{
					var none = _schema.Groups[g].NoneIndex;
					if (none < 0)
						return false;

					bit = _schema.BitOf(g, none);
				}

				bits.Set(bit);
			}

			return true;
		}

		/// <summary>
		/// Per declared group, its first bit (x) and value count (y): the shape of <see cref="CountVariantsJob.Groups"/>.
		/// </summary>
		public void GetDeclaredGroups(List<(int offset, int count)> groups)
		{
			groups.Clear();
			for (var g = 0; g < _present.Length; g++)
			{
				if (_present[g])
					groups.Add((_schema.OffsetOf(g), _schema.Groups[g].Count));
			}
		}
	}
}
