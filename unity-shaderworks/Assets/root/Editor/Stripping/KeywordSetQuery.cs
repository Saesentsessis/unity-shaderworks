using UnityEngine.Rendering;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// <see cref="IKeywordQuery"/> over a compiled variant's keyword set.
	/// </summary>
	internal readonly struct KeywordSetQuery : IKeywordQuery
	{
		private readonly ShaderKeywordSet _set;
		private readonly LocalKeyword[] _slots;

		/// <param name="slots">The pass's local keyword of each slot of the layout.</param>
		public KeywordSetQuery(ShaderKeywordSet set, LocalKeyword[] slots)
		{
			_set = set;
			_slots = slots;
		}

		public bool IsEnabled(int slot) => _set.IsEnabled(_slots[slot]);
	}
}
