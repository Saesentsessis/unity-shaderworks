namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// A compiled <see cref="ExcludedCombination"/>: its pattern and the groups it lists (it only applies where every one
	/// of them is declared).
	/// </summary>
	internal readonly struct ExclusionRule
	{
		public readonly VariantBits Pattern;
		public readonly int[] Groups;

		public ExclusionRule(VariantBits pattern, int[] groups)
		{
			Pattern = pattern;
			Groups = groups;
		}
	}
}
