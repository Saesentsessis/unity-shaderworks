using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// One package's shader stripping settings, stored by the package (typically in a <c>ScriptableSingleton</c> under
	/// <c>ProjectSettings/</c>) and drawn with <see cref="ShaderStrippingSettingsGui"/>.
	/// </summary>
	/// <remarks>
	/// Values, groups and schemas are stored by id, never by position. Entries naming ids the package no longer has are
	/// kept as they are (a package removed for a while doesn't lose its settings) and ignored while unknown.
	/// </remarks>
	[Serializable]
	public sealed class ShaderStrippingSettings
	{
		/// <summary>
		/// Layout version of the serialized data, for future migrations.
		/// </summary>
		public const int CurrentVersion = 1;

		[SerializeField] private int version = CurrentVersion;

		[Tooltip("Automatic: keep what the build's render pipeline assets use, plus the variants of the listed shader variant collections. Custom: keep every combination of the values ticked per keyword group.")]
		[SerializeField] private StrippingMode mode = StrippingMode.Automatic;

		[Tooltip("Automatic: also count render features that are turned off on their renderer (for projects that turn them on from scripts).")]
		[SerializeField] private bool countInactiveSources;

		[Tooltip("Automatic: variants listed in these collections are always kept (for setups the build's render pipeline assets don't show, such as pipeline assets assigned from scripts).")]
		[SerializeField] private List<ShaderVariantCollection> collections = new();

		[Tooltip("Log one summary per shader at the end of each build.")]
		[SerializeField] private bool logSummary = true;

		[Tooltip("Write a JSON report of every build's stripping to Logs/ShaderStripping/.")]
		[SerializeField] private bool writeReport = true;

		[SerializeField] private List<DeniedValues> denied = new();
		[SerializeField] private List<ExcludedCombination> exclusions = new();

		public int Version => version;

		public StrippingMode Mode
		{
			get => mode;
			set => mode = value;
		}

		public bool CountInactiveSources
		{
			get => countInactiveSources;
			set => countInactiveSources = value;
		}

		public bool LogSummary
		{
			get => logSummary;
			set => logSummary = value;
		}

		public bool WriteReport
		{
			get => writeReport;
			set => writeReport = value;
		}

		/// <summary>
		/// Collections whose variants Automatic always keeps (null entries are skipped).
		/// </summary>
		public List<ShaderVariantCollection> Collections => collections ??= new List<ShaderVariantCollection>();

		/// <summary>
		/// Combinations stripped in every mode.
		/// </summary>
		public List<ExcludedCombination> Exclusions => exclusions ??= new List<ExcludedCombination>();

		private List<DeniedValues> Denied => denied ??= new List<DeniedValues>();

		/// <summary>
		/// Custom: whether the value is not kept.
		/// </summary>
		public bool IsDenied(ShaderKeywordSchema schema, int groupIndex, int valueIndex)
		{
			var entry = FindDenied(schema, schema.Groups[groupIndex]);
			if (entry == null)
				return false;

			var value = schema.Groups[groupIndex].Values[valueIndex];
			foreach (var id in entry.ValueIds)
			{
				if (value.Matches(id))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Custom: keeps or drops a value.
		/// </summary>
		public void SetDenied(ShaderKeywordSchema schema, int groupIndex, int valueIndex, bool isDenied)
		{
			var group = schema.Groups[groupIndex];
			var value = group.Values[valueIndex];
			var entry = FindDenied(schema, group);

			if (entry == null)
			{
				if (isDenied == false)
					return;

				entry = new DeniedValues(schema.Id, group.Id);
				Denied.Add(entry);
			}

			entry.Canonicalize(schema, group);
			entry.ValueIds.RemoveAll(value.Matches);

			if (isDenied)
				entry.ValueIds.Add(value.Id);
		}

		/// <summary>
		/// Custom: the pattern of the ticked values (every value of a group without saved choices).
		/// </summary>
		public VariantBits GetCustomPattern(ShaderKeywordSchema schema)
		{
			var pattern = schema.AllValues;
			for (var g = 0; g < schema.Groups.Count; g++)
			{
				var group = schema.Groups[g];
				var kept = group.AllValues;
				for (var v = 0; v < group.Count; v++)
				{
					if (IsDenied(schema, g, v))
						kept &= ~(1UL << v);
				}

				pattern = schema.WithValues(pattern, group, kept);
			}

			return pattern;
		}

		/// <summary>
		/// Custom: ticks exactly the values of <paramref name="pattern"/> (the union of what Automatic keeps), per group.
		/// </summary>
		public void SetCustomPattern(ShaderKeywordSchema schema, in VariantBits pattern)
		{
			for (var g = 0; g < schema.Groups.Count; g++)
			{
				var values = schema.GetValues(pattern, g);
				for (var v = 0; v < schema.Groups[g].Count; v++)
					SetDenied(schema, g, v, (values & (1UL << v)) == 0UL);
			}
		}

		/// <summary>
		/// Adds an empty exclusion (any value in every group) for the schema; it strips nothing until a value is chosen.
		/// </summary>
		public ExcludedCombination AddExclusion(ShaderKeywordSchema schema)
		{
			var exclusion = new ExcludedCombination(schema.Id);
			Exclusions.Add(exclusion);
			return exclusion;
		}

		private DeniedValues FindDenied(ShaderKeywordSchema schema, KeywordGroup group)
		{
			foreach (var entry in Denied)
			{
				if (entry != null && schema.Matches(entry.SchemaId) && group.Matches(entry.GroupId))
					return entry;
			}

			return null;
		}
	}
}
