using System;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// One value of one keyword group, by stored ids (see <see cref="KeywordGroup.Matches"/> and
	/// <see cref="KeywordValue.Matches"/>).
	/// </summary>
	[Serializable]
	public struct GroupValue
	{
		[SerializeField] private string groupId;
		[SerializeField] private string valueId;

		public GroupValue(string groupId, string valueId)
		{
			this.groupId = groupId;
			this.valueId = valueId;
		}

		public readonly string GroupId => groupId;

		public readonly string ValueId => valueId;
	}
}
