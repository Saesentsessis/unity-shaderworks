using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Saesentsessis.Shaders.Stripping.Tests
{
	/// <summary>
	/// The preview's Burst enumeration agrees with a plain managed one.
	/// </summary>
	public class CountVariantsJobTests
	{
		[Test]
		public void Count_MatchesManagedEnumeration()
		{
			var a = SchemaTests.Group("a", 4);
			var b = SchemaTests.Group("b", 1);
			var c = SchemaTests.Group("c", 5);
			var schema = new ShaderKeywordSchema("s", "Hidden/Test", "Test", new[] { a, b, c });

			var keep = new[]
			{
				schema.WithValues(schema.WithValue(schema.AllValues, a, 2), c, 0b110UL),
				schema.WithValue(schema.AllValues, b, 1)
			};
			var exclude = new[] { schema.WithValue(schema.WithValue(schema.AllValues, b, 1), c, 3) };

			var expected = 0L;
			for (var va = 0; va < a.Count; va++)
			for (var vb = 0; vb < b.Count; vb++)
			for (var vc = 0; vc < c.Count; vc++)
			{
				var variant = default(VariantBits);
				variant.Set(schema.BitOf(0, va));
				variant.Set(schema.BitOf(1, vb));
				variant.Set(schema.BitOf(2, vc));
				if (VariantMatching.IsKept(variant, keep, exclude))
					expected++;
			}

			var groups = new NativeArray<int2>(new[]
			{
				new int2(schema.OffsetOf(0), a.Count), new int2(schema.OffsetOf(1), b.Count), new int2(schema.OffsetOf(2), c.Count)
			}, Allocator.TempJob);
			var keepPatterns = new NativeArray<VariantBits>(keep, Allocator.TempJob);
			var excludePatterns = new NativeArray<VariantBits>(exclude, Allocator.TempJob);
			var counts = new NativeReference<VariantCounts>(Allocator.TempJob);

			try
			{
				new CountVariantsJob { Groups = groups, KeepPatterns = keepPatterns, ExcludePatterns = excludePatterns, Counts = counts }.Run();

				Assert.AreEqual(a.Count * b.Count * c.Count, counts.Value.Variants);
				Assert.AreEqual(expected, counts.Value.Kept);
				Assert.Greater(expected, 0);
			}
			finally
			{
				groups.Dispose();
				keepPatterns.Dispose();
				excludePatterns.Dispose();
				counts.Dispose();
			}
		}

		[Test]
		public void Count_OfAPassWithoutGroups_IsOneVariant()
		{
			var schema = new ShaderKeywordSchema("s", "Hidden/Test", "Test", new[] { SchemaTests.Group("a", 1) });
			var groups = new NativeArray<int2>(0, Allocator.TempJob);
			var keep = new NativeArray<VariantBits>(new[] { schema.AllValues }, Allocator.TempJob);
			var exclude = new NativeArray<VariantBits>(0, Allocator.TempJob);
			var counts = new NativeReference<VariantCounts>(Allocator.TempJob);

			try
			{
				new CountVariantsJob { Groups = groups, KeepPatterns = keep, ExcludePatterns = exclude, Counts = counts }.Run();
				Assert.AreEqual(1, counts.Value.Variants);
				Assert.AreEqual(1, counts.Value.Kept);
			}
			finally
			{
				groups.Dispose();
				keep.Dispose();
				exclude.Dispose();
				counts.Dispose();
			}
		}
	}
}
