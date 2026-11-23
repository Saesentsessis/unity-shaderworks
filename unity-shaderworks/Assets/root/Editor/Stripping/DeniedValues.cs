using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// <see cref="StrippingMode.Custom"/>: the values of one keyword group that are not kept. Storing what is denied
	/// (rather than what is kept) keeps any value a package adds later.
	/// </summary>
	[Serializable]
	public sealed class DeniedValues
	{
		[SerializeField] private string schemaId;
		[SerializeField] private string groupId;
		[SerializeField] private List<string> valueIds = new();

		public DeniedValues(string schemaId, string groupId)
		{
			this.schemaId = schemaId;
			this.groupId = groupId;
		}

		public string SchemaId => schemaId;

		public string GroupId => groupId;

		/// <summary>
		/// Stored value ids (keywords, or <see cref="KeywordValue.NoneId"/>).
		/// </summary>
		public List<string> ValueIds => valueIds ??= new List<string>();

		/// <summary>
		/// Rewrites former ids as the current ones (a renamed schema or group), so the entry keeps matching.
		/// </summary>
		internal void Canonicalize(ShaderKeywordSchema schema, KeywordGroup group)
		{
			schemaId = schema.Id;
			groupId = group.Id;
		}
	}
}
