namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// How many variants there were, how many were kept, and how many didn't fit the schema (kept as well).
	/// </summary>
	public struct VariantCounts
	{
		public static VariantCounts operator +(VariantCounts a, VariantCounts b) => new()
		{
			Variants = a.Variants + b.Variants,
			Kept = a.Kept + b.Kept,
			Unrecognized = a.Unrecognized + b.Unrecognized
		};

		public long Variants;
		public long Kept;
		public long Unrecognized;

		public VariantCounts(long variants, long kept, long unrecognized)
		{
			Variants = variants;
			Kept = kept;
			Unrecognized = unrecognized;
		}
	}
}
