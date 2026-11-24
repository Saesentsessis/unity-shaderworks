using System;
using System.Collections.Generic;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// One value of a <see cref="KeywordGroup"/>: a keyword, or none (the <c>_</c> of a <c>multi_compile</c>).
	/// </summary>
	/// <remarks>
	/// Settings store a value by <see cref="Id"/> (its keyword, or <see cref="NoneId"/>), so values can be reordered or
	/// added freely. When a keyword is renamed, list the old name in <c>formerKeywords</c> and saved choices follow it.
	/// </remarks>
	public readonly struct KeywordValue
	{
		/// <summary>
		/// <see cref="Id"/> of the value without a keyword.
		/// </summary>
		public const string NoneId = "_";

		public static KeywordValue None(string displayName) => new(null, displayName, null);

		public static KeywordValue Of(string keyword, string displayName, params string[] formerKeywords)
		{
			if (string.IsNullOrEmpty(keyword) || keyword == NoneId)
				throw new ArgumentException("A keyword value needs a keyword; use KeywordValue.None for '_'.", nameof(keyword));

			return new KeywordValue(keyword, displayName, formerKeywords);
		}

		/// <summary>
		/// The keyword, or null for the value without one.
		/// </summary>
		public readonly string Keyword;

		/// <summary>
		/// Name shown in settings and logs.
		/// </summary>
		public readonly string DisplayName;

		private readonly string[] _formerKeywords;

		private KeywordValue(string keyword, string displayName, string[] formerKeywords)
		{
			Keyword = keyword;
			DisplayName = string.IsNullOrEmpty(displayName) ? keyword ?? "None" : displayName;
			_formerKeywords = formerKeywords is { Length: > 0 } ? (string[])formerKeywords.Clone() : Array.Empty<string>();
		}

		public bool IsNone => Keyword == null;

		/// <summary>
		/// What settings store for this value: the keyword, or <see cref="NoneId"/>.
		/// </summary>
		public string Id => Keyword ?? NoneId;

		/// <summary>
		/// Earlier keywords of this value, still recognized in saved settings.
		/// </summary>
		public IReadOnlyList<string> FormerKeywords => _formerKeywords ?? Array.Empty<string>();

		/// <summary>
		/// Whether a stored id names this value (its id or a former keyword).
		/// </summary>
		public bool Matches(string id)
		{
			if (string.Equals(id, Id, StringComparison.Ordinal))
				return true;

			foreach (var former in FormerKeywords)
			{
				if (string.Equals(id, former, StringComparison.Ordinal))
					return true;
			}

			return false;
		}
	}
}
