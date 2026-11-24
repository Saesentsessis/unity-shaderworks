using Unity.Collections;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// The keep test of every stripping decision: a variant stays if it is a subset of a keep pattern and of no exclusion
	/// pattern. Exclusions are already filtered to those that apply to the variant's pass and stage.
	/// </summary>
	internal static class VariantMatching
	{
		public static bool IsKept(in VariantBits variant, VariantBits[] keepPatterns, VariantBits[] excludePatterns)
		{
			var kept = false;
			for (var p = 0; p < keepPatterns.Length && kept == false; p++)
				kept = variant.IsSubsetOf(keepPatterns[p]);

			if (kept == false)
				return false;

			for (var p = 0; p < excludePatterns.Length; p++)
			{
				if (variant.IsSubsetOf(excludePatterns[p]))
					return false;
			}

			return true;
		}

		/// <summary>
		/// The same test over native arrays, for Burst jobs.
		/// </summary>
		public static bool IsKept(in VariantBits variant, in NativeArray<VariantBits> keepPatterns,
			in NativeArray<VariantBits> excludePatterns)
		{
			var kept = false;
			for (var p = 0; p < keepPatterns.Length && kept == false; p++)
				kept = variant.IsSubsetOf(keepPatterns[p]);

			if (kept == false)
				return false;

			for (var p = 0; p < excludePatterns.Length; p++)
			{
				if (variant.IsSubsetOf(excludePatterns[p]))
					return false;
			}

			return true;
		}
	}
}
