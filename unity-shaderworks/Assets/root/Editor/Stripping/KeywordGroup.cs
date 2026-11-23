using System;
using System.Collections.Generic;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// The keywords of one <c>multi_compile</c> line: in every variant of a pass or stage that declares the group, exactly
	/// one value is on.
	/// </summary>
	public sealed class KeywordGroup
	{
		/// <summary>
		/// Most values a group can have (its values fit one 64-bit word of <see cref="VariantBits"/>).
		/// </summary>
		public const int MaxValues = 64;

		private readonly KeywordValue[] _values;
		private readonly string[] _formerIds;

		/// <param name="id">Stable identifier, stored in settings. When renaming it, list the old one in
		/// <paramref name="formerIds"/>.</param>
		/// <param name="displayName">Name shown in settings and logs.</param>
		/// <param name="values">The values, in display order. Settings store them by keyword, so the order can change.</param>
		/// <param name="formerIds">Earlier ids of the group, still recognized in saved settings.</param>
		public KeywordGroup(string id, string displayName, KeywordValue[] values, params string[] formerIds)
		{
			if (string.IsNullOrEmpty(id))
				throw new ArgumentException("A keyword group needs an id.", nameof(id));

			if (values == null || values.Length == 0 || values.Length > MaxValues)
				throw new ArgumentException($"Keyword group '{id}' needs 1 to {MaxValues} values.", nameof(values));

			var keywords = new HashSet<string>(StringComparer.Ordinal);
			NoneIndex = -1;
			for (var i = 0; i < values.Length; i++)
			{
				if (values[i].Keyword == null && values[i].DisplayName == null)
					throw new ArgumentException($"Keyword group '{id}' has an uninitialized value.", nameof(values));

				if (values[i].IsNone)
				{
					if (NoneIndex >= 0)
						throw new ArgumentException($"Keyword group '{id}' has more than one value without a keyword.", nameof(values));

					NoneIndex = i;
				}
				else if (keywords.Add(values[i].Keyword) == false)
				{
					throw new ArgumentException($"Keyword group '{id}' lists '{values[i].Keyword}' twice.", nameof(values));
				}
			}

			Id = id;
			DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
			_values = (KeywordValue[])values.Clone();
			_formerIds = formerIds is { Length: > 0 } ? (string[])formerIds.Clone() : Array.Empty<string>();
		}

		public string Id { get; }

		public string DisplayName { get; }

		public IReadOnlyList<KeywordValue> Values => _values;

		public int Count => _values.Length;

		/// <summary>
		/// Index of the value without a keyword, or -1 when every value has one.
		/// </summary>
		public int NoneIndex { get; }

		/// <summary>
		/// Earlier ids of the group, still recognized in saved settings.
		/// </summary>
		public IReadOnlyList<string> FormerIds => _formerIds;

		/// <summary>
		/// Every value of the group, as a mask over its value indices.
		/// </summary>
		public ulong AllValues => Count == MaxValues ? ulong.MaxValue : (1UL << Count) - 1UL;

		/// <summary>
		/// Whether a stored id names this group (its id or a former one).
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

		/// <returns>The index of the value with <paramref name="keyword"/> (null: the value without one), or -1.</returns>
		public int IndexOf(string keyword)
		{
			for (var i = 0; i < _values.Length; i++)
			{
				if (string.Equals(_values[i].Keyword, keyword, StringComparison.Ordinal))
					return i;
			}

			return -1;
		}

		/// <returns>The index of the value a stored id names (its keyword, a former keyword, or
		/// <see cref="KeywordValue.NoneId"/>), or -1.</returns>
		public int IndexOfId(string id)
		{
			for (var i = 0; i < _values.Length; i++)
			{
				if (_values[i].Matches(id))
					return i;
			}

			return -1;
		}
	}
}
