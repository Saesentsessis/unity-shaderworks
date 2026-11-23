using System;
using System.Collections.Generic;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// The keyword groups of one shader that stripping understands, and where each value sits in a
	/// <see cref="VariantBits"/>. Keywords outside the schema are ignored: variants differing only in them share a fate.
	/// </summary>
	/// <remarks>
	/// Groups are laid out in order, each in one 64-bit word (a group that doesn't fit what is left of a word starts the
	/// next one), at most <see cref="VariantBits.Capacity"/> bits in all.
	/// </remarks>
	public sealed class ShaderKeywordSchema
	{
		private readonly KeywordGroup[] _groups;
		private readonly int[] _offsets;
		private readonly string[] _formerIds;
		private readonly Dictionary<string, (int group, int value)> _keywords = new(StringComparer.Ordinal);

		/// <param name="id">Stable identifier, stored in settings. When renaming it, list the old one in
		/// <paramref name="formerIds"/>.</param>
		/// <param name="shaderName">The shader's name, as <c>Shader.Find</c> takes it.</param>
		/// <param name="displayName">Name shown in settings and logs.</param>
		/// <param name="groups">The keyword groups stripping understands.</param>
		/// <param name="formerIds">Earlier ids of the schema, still recognized in saved settings.</param>
		public ShaderKeywordSchema(string id, string shaderName, string displayName, KeywordGroup[] groups,
			params string[] formerIds)
		{
			if (string.IsNullOrEmpty(id))
				throw new ArgumentException("A schema needs an id.", nameof(id));

			if (string.IsNullOrEmpty(shaderName))
				throw new ArgumentException($"Schema '{id}' needs a shader name.", nameof(shaderName));

			if (groups == null || groups.Length == 0)
				throw new ArgumentException($"Schema '{id}' needs at least one keyword group.", nameof(groups));

			Id = id;
			ShaderName = shaderName;
			DisplayName = string.IsNullOrEmpty(displayName) ? shaderName : displayName;
			_groups = (KeywordGroup[])groups.Clone();
			_offsets = new int[_groups.Length];
			_formerIds = formerIds is { Length: > 0 } ? (string[])formerIds.Clone() : Array.Empty<string>();

			var groupIds = new HashSet<string>(StringComparer.Ordinal);
			var bit = 0;
			for (var g = 0; g < _groups.Length; g++)
			{
				var group = _groups[g] ?? throw new ArgumentException($"Schema '{id}' has a null group.", nameof(groups));
				if (groupIds.Add(group.Id) == false)
					throw new ArgumentException($"Schema '{id}' lists group '{group.Id}' twice.", nameof(groups));

				// A group never crosses a word, so its mask is one shift away.
				if ((bit & 63) + group.Count > 64)
					bit = (bit + 63) & ~63;

				_offsets[g] = bit;
				bit += group.Count;

				for (var v = 0; v < group.Count; v++)
				{
					var keyword = group.Values[v].Keyword;
					if (keyword != null && _keywords.TryAdd(keyword, (g, v)) == false)
						throw new ArgumentException($"Schema '{id}' has keyword '{keyword}' in two groups.", nameof(groups));
				}
			}

			if (bit > VariantBits.Capacity)
				throw new ArgumentException($"Schema '{id}' needs {bit} bits, more than {VariantBits.Capacity}.", nameof(groups));

			BitCount = bit;

			var all = default(VariantBits);
			for (var g = 0; g < _groups.Length; g++)
				all.SetRange(_offsets[g], _groups[g].Count, _groups[g].AllValues);

			AllValues = all;
		}

		public string Id { get; }

		public string ShaderName { get; }

		public string DisplayName { get; }

		public IReadOnlyList<KeywordGroup> Groups => _groups;

		/// <summary>
		/// Bits the layout spans, padding included.
		/// </summary>
		public int BitCount { get; }

		/// <summary>
		/// The pattern allowing every value of every group: keeps everything.
		/// </summary>
		public VariantBits AllValues { get; }

		/// <returns>The group's index, or -1.</returns>
		public int IndexOf(KeywordGroup group) => Array.IndexOf(_groups, group);

		/// <returns>The index of the group a stored id names (its id or a former one), or -1.</returns>
		public int IndexOf(string groupId)
		{
			for (var g = 0; g < _groups.Length; g++)
			{
				if (_groups[g].Matches(groupId))
					return g;
			}

			return -1;
		}

		/// <summary>
		/// Whether a stored id names this schema (its id or a former one).
		/// </summary>
		public bool Matches(string id)
		{
			if (string.Equals(id, Id, StringComparison.Ordinal))
				return true;

			foreach (var former in _formerIds)
			{
				if (string.Equals(id, former, StringComparison.Ordinal))
					return true;
			}

			return false;
		}

		/// <summary>
		/// First bit of the group.
		/// </summary>
		public int OffsetOf(int groupIndex) => _offsets[groupIndex];

		public int BitOf(int groupIndex, int valueIndex) => _offsets[groupIndex] + valueIndex;

		/// <summary>
		/// Which group and value <paramref name="keyword"/> belongs to.
		/// </summary>
		public bool TryFindKeyword(string keyword, out int groupIndex, out int valueIndex)
		{
			if (keyword != null && _keywords.TryGetValue(keyword, out var found))
			{
				(groupIndex, valueIndex) = found;
				return true;
			}

			groupIndex = valueIndex = -1;
			return false;
		}

		/// <summary>
		/// The values of <paramref name="groupIndex"/> that <paramref name="bits"/> contains, as a mask over value indices.
		/// </summary>
		public ulong GetValues(in VariantBits bits, int groupIndex) => bits.GetRange(_offsets[groupIndex], _groups[groupIndex].Count);

		/// <summary>
		/// <paramref name="pattern"/> with <paramref name="group"/> limited to the values in <paramref name="valueMask"/>
		/// (bit i = value i), the other groups unchanged.
		/// </summary>
		public VariantBits WithValues(VariantBits pattern, KeywordGroup group, ulong valueMask)
		{
			var g = RequireIndex(group);
			pattern.SetRange(_offsets[g], _groups[g].Count, valueMask & _groups[g].AllValues);
			return pattern;
		}

		/// <summary>
		/// <paramref name="pattern"/> with <paramref name="group"/> limited to the value at <paramref name="valueIndex"/>.
		/// </summary>
		public VariantBits WithValue(VariantBits pattern, KeywordGroup group, int valueIndex)
		{
			var g = RequireIndex(group);
			if ((uint)valueIndex >= (uint)_groups[g].Count)
				throw new ArgumentOutOfRangeException(nameof(valueIndex));

			return WithValues(pattern, group, 1UL << valueIndex);
		}

		/// <summary>
		/// <paramref name="pattern"/> with <paramref name="group"/> limited to the value of <paramref name="keyword"/> (null:
		/// the value without a keyword).
		/// </summary>
		public VariantBits WithKeyword(VariantBits pattern, KeywordGroup group, string keyword)
		{
			var index = keyword == null ? group.NoneIndex : group.IndexOf(keyword);
			if (index < 0)
				throw new ArgumentException($"Group '{group.Id}' has no value '{keyword ?? "none"}'.", nameof(keyword));

			return WithValue(pattern, group, index);
		}

		/// <summary>
		/// The exact variant a list of enabled keywords names (a shader variant collection entry): each group gets the
		/// value whose keyword is listed, else its value without a keyword, else every value (unknown). Keywords outside
		/// the schema are ignored.
		/// </summary>
		public VariantBits FromKeywords(IEnumerable<string> keywords)
		{
			var listed = new ulong[_groups.Length];
			foreach (var keyword in keywords)
			{
				if (TryFindKeyword(keyword, out var g, out var v))
					listed[g] |= 1UL << v;
			}

			var bits = default(VariantBits);
			for (var g = 0; g < _groups.Length; g++)
			{
				var group = _groups[g];
				var mask = listed[g] != 0UL ? listed[g]
					: group.NoneIndex >= 0 ? 1UL << group.NoneIndex
					: group.AllValues;

				bits.SetRange(_offsets[g], group.Count, mask);
			}

			return bits;
		}

		public override string ToString() => Id;

		private int RequireIndex(KeywordGroup group)
		{
			var g = IndexOf(group);
			if (g < 0)
				throw new ArgumentException($"Group '{group?.Id}' isn't part of schema '{Id}'.", nameof(group));

			return g;
		}
	}
}
