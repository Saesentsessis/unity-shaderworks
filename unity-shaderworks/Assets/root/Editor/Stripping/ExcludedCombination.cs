using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// A partial combination stripped in every mode: one value for some keyword groups, any value for the others. A
	/// variant matching every listed value is stripped. It only applies to passes and stages that declare every group it
	/// lists.
	/// </summary>
	[Serializable]
	public sealed class ExcludedCombination
	{
		[SerializeField] private string schemaId;
		[SerializeField] private List<GroupValue> values = new();

		public ExcludedCombination(string schemaId)
		{
			this.schemaId = schemaId;
		}

		public string SchemaId => schemaId;

		/// <summary>
		/// The listed values; groups not listed match any value.
		/// </summary>
		public List<GroupValue> Values => values ??= new List<GroupValue>();

		/// <returns>The value index chosen for the group, or -1 for any value (also when the stored value no longer
		/// exists).</returns>
		public int GetValue(ShaderKeywordSchema schema, int groupIndex)
		{
			var group = schema.Groups[groupIndex];
			foreach (var value in Values)
			{
				if (group.Matches(value.GroupId))
					return group.IndexOfId(value.ValueId);
			}

			return -1;
		}

		/// <summary>
		/// Chooses a value for the group (-1: any value).
		/// </summary>
		public void SetValue(ShaderKeywordSchema schema, int groupIndex, int valueIndex)
		{
			var group = schema.Groups[groupIndex];
			Values.RemoveAll(value => group.Matches(value.GroupId));

			if (valueIndex >= 0)
				Values.Add(new GroupValue(group.Id, group.Values[valueIndex].Id));

			schemaId = schema.Id;
		}

		/// <summary>
		/// The combination as an exclusion pattern: the listed groups limited to their value, the others any value.
		/// </summary>
		/// <param name="constrainedGroups">The listed groups' indices.</param>
		/// <returns>False when a listed group or value no longer exists in the schema: the entry then strips nothing
		/// (never strip on a guess).</returns>
		public bool TryGetPattern(ShaderKeywordSchema schema, out VariantBits pattern, List<int> constrainedGroups)
		{
			pattern = schema.AllValues;
			constrainedGroups.Clear();

			foreach (var value in Values)
			{
				var g = schema.IndexOf(value.GroupId);
				if (g < 0)
					return false;

				var v = schema.Groups[g].IndexOfId(value.ValueId);
				if (v < 0)
					return false;

				pattern = schema.WithValue(pattern, schema.Groups[g], v);
				if (constrainedGroups.Contains(g) == false)
					constrainedGroups.Add(g);
			}

			return constrainedGroups.Count > 0;
		}
	}
}
