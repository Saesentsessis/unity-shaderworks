using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// What one source's settings keep and exclude for one schema, for one build (or one settings preview). Fails open: if
	/// anything can't be read or trusted, it keeps every variant and says why.
	/// </summary>
	internal sealed class StrippingRules
	{
		/// <param name="mode">The mode to build the rules for (the settings' own mode in builds; the settings page also
		/// asks what Automatic would keep while Custom is selected).</param>
		/// <param name="shader">The schema's shader, to check its keywords and read shader variant collections; null
		/// skips both.</param>
		public static StrippingRules Create(IShaderStrippingSource source, ShaderStrippingSettings settings, StrippingMode mode,
			ShaderKeywordSchema schema, StrippingContext context, Shader shader)
		{
			var names = new List<string>();

			if (shader != null && TryFindMissingKeyword(schema, shader, out var missing))
				return KeepEverything(schema, mode, names, $"the shader doesn't declare keyword '{missing}' of the schema");

			var keep = new List<VariantBits>();
			if (mode == StrippingMode.Custom)
			{
				keep.Add(settings.GetCustomPattern(schema));
			}
			else
			{
				try
				{
					source.CollectAutomaticPatterns(context, schema, keep, names);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
					return KeepEverything(schema, mode, names, $"the source failed ({exception.GetType().Name}: {exception.Message})");
				}

				if (shader != null && TryAddCollections(settings, schema, shader, keep, names, out var error) == false)
					return KeepEverything(schema, mode, names, error);

				if (keep.Count == 0)
					return KeepEverything(schema, mode, names, "nothing in the build's render pipeline assets uses it");
			}

			return new StrippingRules(schema, mode, names, keep.ToArray(), CompileExclusions(settings, schema), null);
		}

		private static StrippingRules KeepEverything(ShaderKeywordSchema schema, StrippingMode mode, List<string> names, string reason)
			=> new(schema, mode, names, new[] { schema.AllValues }, Array.Empty<ExclusionRule>(), reason);

		private static bool TryFindMissingKeyword(ShaderKeywordSchema schema, Shader shader, out string missing)
		{
			var space = shader.keywordSpace;
			foreach (var group in schema.Groups)
			foreach (var value in group.Values)
			{
				if (value.Keyword != null && space.FindKeyword(value.Keyword).isValid == false)
				{
					missing = value.Keyword;
					return true;
				}
			}

			missing = null;
			return false;
		}

		private static bool TryAddCollections(ShaderStrippingSettings settings, ShaderKeywordSchema schema, Shader shader,
			List<VariantBits> keep, List<string> names, out string error)
		{
			error = null;
			var keywordSets = new List<string[]>();
			foreach (var collection in settings.Collections)
			{
				if (collection == null)
					continue;

				keywordSets.Clear();
				if (ShaderVariantCollectionReader.TryRead(collection, shader, keywordSets, out var readError) == false)
				{
					error = $"shader variant collection '{collection.name}' couldn't be read ({readError})";
					return false;
				}

				foreach (var keywords in keywordSets)
					keep.Add(schema.FromKeywords(keywords));

				if (keywordSets.Count > 0)
					names.Add($"{collection.name} ({keywordSets.Count} variants)");
			}

			return true;
		}

		private static ExclusionRule[] CompileExclusions(ShaderStrippingSettings settings, ShaderKeywordSchema schema)
		{
			var rules = new List<ExclusionRule>();
			var groups = new List<int>();
			foreach (var exclusion in settings.Exclusions)
			{
				if (exclusion == null || schema.Matches(exclusion.SchemaId) == false)
					continue;

				if (exclusion.TryGetPattern(schema, out var pattern, groups))
					rules.Add(new ExclusionRule(pattern, groups.ToArray()));
			}

			return rules.ToArray();
		}

		private StrippingRules(ShaderKeywordSchema schema, StrippingMode mode, List<string> sourceNames, VariantBits[] keep,
			ExclusionRule[] exclusions, string keepEverythingReason)
		{
			Schema = schema;
			Mode = mode;
			SourceNames = sourceNames;
			Keep = keep;
			Exclusions = exclusions;
			KeepEverythingReason = keepEverythingReason;
		}

		public ShaderKeywordSchema Schema { get; }

		public StrippingMode Mode { get; }

		/// <summary>
		/// What produced the keep patterns (renderer features, collections), for logs.
		/// </summary>
		public IReadOnlyList<string> SourceNames { get; }

		public VariantBits[] Keep { get; }

		public ExclusionRule[] Exclusions { get; }

		/// <summary>
		/// Why every variant is kept, or null when the rules strip.
		/// </summary>
		public string KeepEverythingReason { get; }

		public bool KeepsEverything => KeepEverythingReason != null;

		/// <summary>
		/// The union of the keep patterns: per group, every value some pattern keeps (Custom's "import from Automatic").
		/// </summary>
		public VariantBits KeptValues
		{
			get
			{
				var union = default(VariantBits);
				foreach (var pattern in Keep)
					union |= pattern;

				return union;
			}
		}
	}
}
