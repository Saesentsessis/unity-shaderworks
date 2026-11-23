using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// Enumerates every variant of one pass (each combination of the values of the groups it declares) and counts how
	/// many the rules keep: the settings page's preview, without compiling anything. The count grows with the product of
	/// the groups' sizes, which is where Burst pays off.
	/// </summary>
	[BurstCompile]
	internal struct CountVariantsJob : IJob
	{
		/// <summary>
		/// Per group the pass declares: first bit (x) and value count (y).
		/// </summary>
		[ReadOnly] public NativeArray<int2> Groups;

		[ReadOnly] public NativeArray<VariantBits> KeepPatterns;

		/// <summary>
		/// The exclusions that apply to the pass.
		/// </summary>
		[ReadOnly] public NativeArray<VariantBits> ExcludePatterns;

		/// <summary>
		/// Variants of the pass and kept ones.
		/// </summary>
		[WriteOnly] public NativeReference<VariantCounts> Counts;

		public void Execute()
		{
			var digits = new NativeArray<int>(math.max(Groups.Length, 1), Allocator.Temp);
			long total = 0, kept = 0;

			// Mixed-radix counter over the groups: one variant per step.
			while (true)
			{
				var variant = default(VariantBits);
				for (var g = 0; g < Groups.Length; g++)
					variant.Set(Groups[g].x + digits[g]);

				total++;
				if (VariantMatching.IsKept(variant, KeepPatterns, ExcludePatterns))
					kept++;

				var carry = 0;
				for (; carry < Groups.Length; carry++)
				{
					if (++digits[carry] < Groups[carry].y)
						break;

					digits[carry] = 0;
				}

				if (carry == Groups.Length)
					break;
			}

			Counts.Value = new VariantCounts(total, kept, 0);
		}
	}
}
