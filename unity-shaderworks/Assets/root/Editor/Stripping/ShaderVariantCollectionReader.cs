using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// Reads the keyword sets a shader variant collection lists for one shader. Unity has no public API to enumerate a
	/// collection, so this reads its serialized data (<c>m_Shaders</c>: shader, then <c>variants</c> with
	/// <c>keywords</c>), a layout unchanged since Unity 5.
	/// </summary>
	internal static class ShaderVariantCollectionReader
	{
		/// <summary>
		/// Adds the keyword sets listed for <paramref name="shader"/> (pass types are ignored: a set kept for one pass type
		/// is kept for all).
		/// </summary>
		/// <returns>False when the collection couldn't be read; <paramref name="error"/> says why.</returns>
		public static bool TryRead(ShaderVariantCollection collection, Shader shader, List<string[]> keywordSets,
			out string error)
		{
			error = null;
			try
			{
				using var serialized = new SerializedObject(collection);
				var shaders = serialized.FindProperty("m_Shaders");
				if (shaders == null || shaders.isArray == false)
				{
					error = "no m_Shaders array";
					return false;
				}

				for (var i = 0; i < shaders.arraySize; i++)
				{
					var entry = shaders.GetArrayElementAtIndex(i);
					if (entry.FindPropertyRelative("first")?.objectReferenceValue != shader)
						continue;

					var variants = entry.FindPropertyRelative("second.variants");
					if (variants == null || variants.isArray == false)
					{
						error = "no variants array";
						return false;
					}

					for (var v = 0; v < variants.arraySize; v++)
					{
						var keywords = variants.GetArrayElementAtIndex(v).FindPropertyRelative("keywords")?.stringValue ?? string.Empty;
						keywordSets.Add(keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries));
					}
				}

				return true;
			}
			catch (Exception exception)
			{
				error = exception.Message;
				return false;
			}
		}
	}
}
