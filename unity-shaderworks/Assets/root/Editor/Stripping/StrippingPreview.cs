using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// What a build for a target would keep of one schema's shader, without building: every variant of every pass
	/// (Burst job), checked against the rules.
	/// </summary>
	public sealed class StrippingPreview
	{
		/// <param name="mode">The mode to preview (Automatic while Custom is selected answers "Import from Automatic").</param>
		public static StrippingPreview Create(IShaderStrippingSource source, ShaderStrippingSettings settings, StrippingMode mode,
			ShaderKeywordSchema schema, BuildTarget target)
		{
			var shader = Shader.Find(schema.ShaderName);
			var context = new StrippingContext(target, StrippingContext.GetPipelineAssets(target), settings.CountInactiveSources);
			var rules = StrippingRules.Create(source, settings, mode, schema, context, shader);

			if (shader == null)
				return new StrippingPreview(schema, rules, default, $"shader '{schema.ShaderName}' wasn't found");

			var counts = default(VariantCounts);
			var groups = new List<(int offset, int count)>();
			var keep = new NativeArray<VariantBits>(rules.Keep, Allocator.TempJob);
			var result = new NativeReference<VariantCounts>(Allocator.TempJob);

			try
			{
				for (var subshader = 0; subshader < shader.subshaderCount; subshader++)
				for (var pass = 0; pass < shader.GetPassCountInSubshader(subshader); pass++)
				{
					var passId = new PassIdentifier((uint)subshader, (uint)pass);
					var names = new HashSet<string>(System.StringComparer.Ordinal);
					foreach (var keyword in ShaderUtil.GetPassKeywords(shader, passId))
						names.Add(keyword.name);

					var layout = PassLayout.Create(schema, names);
					layout.ApplyExclusions(rules.Exclusions);
					layout.GetDeclaredGroups(groups);

					var declared = new NativeArray<int2>(groups.Count, Allocator.TempJob);
					var exclusions = new NativeArray<VariantBits>(layout.Exclusions, Allocator.TempJob);
					try
					{
						for (var g = 0; g < groups.Count; g++)
							declared[g] = new int2(groups[g].offset, groups[g].count);

						new CountVariantsJob
						{
							Groups = declared,
							KeepPatterns = keep,
							ExcludePatterns = exclusions,
							Counts = result
						}.Run();

						counts += result.Value;
					}
					finally
					{
						declared.Dispose();
						exclusions.Dispose();
					}
				}
			}
			finally
			{
				keep.Dispose();
				result.Dispose();
			}

			return new StrippingPreview(schema, rules, counts, null);
		}

		private readonly StrippingRules _rules;

		private StrippingPreview(ShaderKeywordSchema schema, StrippingRules rules, VariantCounts counts, string error)
		{
			Schema = schema;
			_rules = rules;
			Counts = counts;
			Error = error;
		}

		public ShaderKeywordSchema Schema { get; }

		/// <summary>
		/// Variants over every pass (one per keyword combination; stages and graphics APIs multiply both numbers alike).
		/// </summary>
		public VariantCounts Counts { get; }

		/// <summary>
		/// Why the preview couldn't be computed, or null.
		/// </summary>
		public string Error { get; }

		/// <summary>
		/// Why a build would keep every variant, or null.
		/// </summary>
		public string KeepEverythingReason => _rules.KeepEverythingReason;

		/// <summary>
		/// What produced the keep patterns.
		/// </summary>
		public IReadOnlyList<string> SourceNames => _rules.SourceNames;

		/// <summary>
		/// Per group, every value some keep pattern keeps.
		/// </summary>
		public VariantBits KeptValues => _rules.KeptValues;
	}
}
