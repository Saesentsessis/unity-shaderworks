using System.Collections.Generic;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// <see cref="IKeywordQuery"/> over a set of enabled keyword names (shader variant collections, tests).
	/// </summary>
	internal readonly struct KeywordNameQuery : IKeywordQuery
	{
		private readonly ICollection<string> _enabled;
		private readonly IReadOnlyList<string> _slots;

		public KeywordNameQuery(ICollection<string> enabled, IReadOnlyList<string> slotKeywords)
		{
			_enabled = enabled;
			_slots = slotKeywords;
		}

		public bool IsEnabled(int slot) => _enabled.Contains(_slots[slot]);
	}
}
