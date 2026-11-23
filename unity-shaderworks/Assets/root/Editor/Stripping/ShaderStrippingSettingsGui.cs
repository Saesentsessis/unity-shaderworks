using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// Draws one package's <see cref="ShaderStrippingSettings"/> (IMGUI), for its Project Settings page: the mode, the
	/// Automatic options, Custom's values per keyword group, the excluded combinations, and a preview of what a build for
	/// the active target would keep. Keep one instance per page (it holds foldouts and the cached preview).
	/// </summary>
	public sealed class ShaderStrippingSettingsGui
	{
		private const string AnyValue = "Any";
		private const float ButtonWidth = 24f;
		private const string NoEffectNote = "Strips nothing until a value is picked for at least one group.";

		private static readonly GUIContent HeaderLabel = new("Shader Variant Stripping",
			"Which variants of this package's shaders player builds compile. Only affects builds: the Editor always has every variant.");
		private static readonly GUIContent ModeLabel = new("Mode",
			"Automatic: keep what the build's render pipeline assets use, plus the variants of the listed shader variant collections. Custom: keep every combination of the values ticked below.");
		private static readonly GUIContent CountInactiveLabel = new("Count Inactive Features",
			"Also keep the variants of render features turned off on their renderer, for projects that turn them on from scripts.");
		private static readonly GUIContent CollectionsLabel = new("Shader Variant Collections",
			"Variants listed in these collections are always kept in Automatic mode: setups the build's render pipeline assets don't show, such as pipeline assets assigned from scripts.");
		private static readonly GUIContent ExclusionsLabel = new("Excluded Combinations",
			"Stripped in every mode: each entry picks a value for some keyword groups (Any for the others), and every variant with all of them is stripped. An entry only applies to passes that have every group it picks.");
		private static readonly GUIContent ImportLabel = new("Import from Automatic",
			"Tick exactly the values Automatic would keep for the active build target.");
		private static readonly GUIContent LogSummaryLabel = new("Log Summary", "Log one line per shader at the end of each build.");
		private static readonly GUIContent WriteReportLabel = new("Write Report", "Write a JSON report of each build's stripping to Logs/ShaderStripping/.");
		private static readonly GUIContent RefreshLabel = new("Refresh", "Recompute the preview (render pipeline assets and features may have changed).");
		private static readonly GUIContent AddLabel = new("+", "Add");
		private static readonly GUIContent RemoveLabel = new("-", "Remove");

		private static void Record(Object owner, string name)
		{
			if (owner != null)
				Undo.RecordObject(owner, name);
		}

		private static bool DrawToggle(Object owner, GUIContent label, bool value, Action<bool> apply)
		{
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUILayout.Toggle(label, value);
			if (EditorGUI.EndChangeCheck() == false)
				return false;

			Record(owner, "Change " + label.text);
			apply(newValue);
			return true;
		}

		/// <summary>
		/// A list's add button, right-aligned under its remove buttons.
		/// </summary>
		private static bool AddButton()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				return GUILayout.Button(AddLabel, GUILayout.Width(ButtonWidth));
			}
		}

		private static ShaderKeywordSchema FindSchema(IShaderStrippingSource source, string schemaId)
		{
			if (schemaId == null)
				return null;

			foreach (var schema in source.Schemas)
			{
				if (schema.Matches(schemaId))
					return schema;
			}

			return null;
		}

		private readonly Dictionary<string, bool> _foldouts = new();
		private readonly List<StrippingPreview> _previews = new();

		/// <summary>
		/// Scratch list for <see cref="ExcludedCombination.TryGetPattern"/>, reused every draw.
		/// </summary>
		private readonly List<int> _exclusionGroups = new();
		private bool _previewStale = true;

		/// <summary>
		/// A change made outside <see cref="Draw"/> (a context menu), reported by the next call.
		/// </summary>
		private bool _pendingChange;

		/// <summary>
		/// Draws the settings; changes are recorded for Undo on <paramref name="owner"/>.
		/// </summary>
		/// <returns>Whether anything changed (the caller saves).</returns>
		public bool Draw(Object owner, ShaderStrippingSettings settings, IShaderStrippingSource source)
		{
			var changed = _pendingChange;
			_pendingChange = false;

			EditorGUILayout.LabelField(HeaderLabel, EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope())
			{
				EditorGUI.BeginChangeCheck();
				var mode = (StrippingMode)EditorGUILayout.EnumPopup(ModeLabel, settings.Mode);
				if (EditorGUI.EndChangeCheck())
				{
					Record(owner, "Change Stripping Mode");
					settings.Mode = mode;
					changed = true;
				}

				changed |= settings.Mode == StrippingMode.Automatic
					? DrawAutomatic(owner, settings)
					: DrawCustom(owner, settings, source);

				EditorGUILayout.Space();
				changed |= DrawExclusions(owner, settings, source);

				EditorGUILayout.Space();
				changed |= DrawToggle(owner, LogSummaryLabel, settings.LogSummary, value => settings.LogSummary = value);
				changed |= DrawToggle(owner, WriteReportLabel, settings.WriteReport, value => settings.WriteReport = value);

				if (changed)
					_previewStale = true;

				EditorGUILayout.Space();
				DrawPreview(settings, source);
			}

			return changed;
		}

		/// <summary>
		/// Recomputes the preview on the next draw (after Undo, or when something outside the settings changed).
		/// </summary>
		public void MarkPreviewStale() => _previewStale = true;

		private bool DrawAutomatic(Object owner, ShaderStrippingSettings settings)
		{
			var changed = DrawToggle(owner, CountInactiveLabel, settings.CountInactiveSources, value => settings.CountInactiveSources = value);

			EditorGUILayout.LabelField(CollectionsLabel);
			using (new EditorGUI.IndentLevelScope())
			{
				var collections = settings.Collections;
				var removed = -1;
				for (var i = 0; i < collections.Count; i++)
				{
					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUI.BeginChangeCheck();
						var collection = (ShaderVariantCollection)EditorGUILayout.ObjectField(collections[i], typeof(ShaderVariantCollection), false);
						if (EditorGUI.EndChangeCheck())
						{
							Record(owner, "Change Shader Variant Collection");
							collections[i] = collection;
							changed = true;
						}

						if (GUILayout.Button(RemoveLabel, GUILayout.Width(ButtonWidth)))
							removed = i;
					}
				}

				if (removed >= 0)
				{
					Record(owner, "Remove Shader Variant Collection");
					collections.RemoveAt(removed);
					changed = true;
				}

				if (AddButton())
				{
					Record(owner, "Add Shader Variant Collection");
					collections.Add(null);
					changed = true;
				}
			}

			return changed;
		}

		private bool DrawCustom(Object owner, ShaderStrippingSettings settings, IShaderStrippingSource source)
		{
			var changed = false;
			foreach (var schema in source.Schemas)
			{
				if (Foldout(schema.Id, schema.DisplayName) == false)
					continue;

				using (new EditorGUI.IndentLevelScope())
				{
					for (var g = 0; g < schema.Groups.Count; g++)
					{
						var group = schema.Groups[g];
						EditorGUILayout.LabelField(group.DisplayName);
						using (new EditorGUI.IndentLevelScope())
						{
							for (var v = 0; v < group.Count; v++)
							{
								EditorGUI.BeginChangeCheck();
								var keep = EditorGUILayout.ToggleLeft(group.Values[v].DisplayName, settings.IsDenied(schema, g, v) == false);
								if (EditorGUI.EndChangeCheck())
								{
									Record(owner, "Change Kept Keyword Values");
									settings.SetDenied(schema, g, v, keep == false);
									changed = true;
								}
							}
						}
					}

					using (new EditorGUILayout.HorizontalScope())
					{
						GUILayout.Space(EditorGUI.indentLevel * 15f);
						if (GUILayout.Button(ImportLabel, GUILayout.ExpandWidth(false)))
						{
							var automatic = StrippingPreview.Create(source, settings, StrippingMode.Automatic, schema,
								EditorUserBuildSettings.activeBuildTarget);
							Record(owner, "Import Stripping from Automatic");
							settings.SetCustomPattern(schema, automatic.KeptValues);
							changed = true;
						}
					}
				}
			}

			return changed;
		}

		private bool DrawExclusions(Object owner, ShaderStrippingSettings settings, IShaderStrippingSource source)
		{
			var changed = false;
			EditorGUILayout.LabelField(ExclusionsLabel);

			using (new EditorGUI.IndentLevelScope())
			{
				var exclusions = settings.Exclusions;
				var removed = -1;
				for (var i = 0; i < exclusions.Count; i++)
				{
					var exclusion = exclusions[i];
					var schema = FindSchema(source, exclusion?.SchemaId);

					using (new EditorGUILayout.HorizontalScope())
					{
						var title = schema != null ? schema.DisplayName : $"Unknown shader ({exclusion?.SchemaId}), kept as is";
						EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
						if (GUILayout.Button(RemoveLabel, GUILayout.Width(ButtonWidth)))
							removed = i;
					}

					if (schema == null)
						continue;

					using (new EditorGUI.IndentLevelScope())
					{
						for (var g = 0; g < schema.Groups.Count; g++)
						{
							var group = schema.Groups[g];
							var options = new string[group.Count + 1];
							options[0] = AnyValue;
							for (var v = 0; v < group.Count; v++)
								options[v + 1] = group.Values[v].DisplayName;

							EditorGUI.BeginChangeCheck();
							var selected = EditorGUILayout.Popup(group.DisplayName, exclusion.GetValue(schema, g) + 1, options) - 1;
							if (EditorGUI.EndChangeCheck())
							{
								Record(owner, "Change Excluded Combination");
								exclusion.SetValue(schema, g, selected);
								changed = true;
							}
						}

						// Any everywhere, or a picked value the shader no longer has.
						if (exclusion.TryGetPattern(schema, out _, _exclusionGroups) == false)
							EditorGUILayout.HelpBox(NoEffectNote, MessageType.None);
					}
				}

				if (removed >= 0)
				{
					Record(owner, "Remove Excluded Combination");
					exclusions.RemoveAt(removed);
					changed = true;
				}

				if (AddButton())
					changed |= AddExclusion(owner, settings, source);
			}

			return changed;
		}

		/// <returns>Whether the exclusion was added now (with several schemas, a menu adds it later).</returns>
		private bool AddExclusion(Object owner, ShaderStrippingSettings settings, IShaderStrippingSource source)
		{
			if (source.Schemas.Count == 1)
			{
				Record(owner, "Add Excluded Combination");
				settings.AddExclusion(source.Schemas[0]);
				return true;
			}

			var menu = new GenericMenu();
			foreach (var schema in source.Schemas)
			{
				menu.AddItem(new GUIContent(schema.DisplayName), false, () =>
				{
					Record(owner, "Add Excluded Combination");
					settings.AddExclusion(schema);
					_pendingChange = true;
				});
			}

			menu.ShowAsContext();
			return false;
		}

		private void DrawPreview(ShaderStrippingSettings settings, IShaderStrippingSource source)
		{
			var target = EditorUserBuildSettings.activeBuildTarget;
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField($"Preview ({target})", EditorStyles.miniBoldLabel);
				if (GUILayout.Button(RefreshLabel, GUILayout.ExpandWidth(false)))
					_previewStale = true;
			}

			// Recomputed on Layout only, so Layout and Repaint draw the same controls.
			if (_previewStale && Event.current.type == EventType.Layout)
			{
				_previews.Clear();
				foreach (var schema in source.Schemas)
					_previews.Add(StrippingPreview.Create(source, settings, settings.Mode, schema, target));

				_previewStale = false;
			}

			using (new EditorGUI.IndentLevelScope())
			{
				foreach (var preview in _previews)
				{
					var type = MessageType.Info;
					string text;
					if (preview.Error != null)
					{
						text = $"{preview.Schema.DisplayName}: {preview.Error}.";
						type = MessageType.Warning;
					}
					else if (preview.KeepEverythingReason != null)
					{
						text = $"{preview.Schema.DisplayName}: keeps all {preview.Counts.Variants} variants ({preview.KeepEverythingReason}).";
						type = MessageType.Warning;
					}
					else
					{
						text = $"{preview.Schema.DisplayName}: keeps {preview.Counts.Kept} of {preview.Counts.Variants} variants (per stage and graphics API).";
						if (preview.SourceNames.Count > 0)
							text += "\nFor: " + string.Join(", ", preview.SourceNames);

						if (preview.Counts.Kept == 0)
							type = MessageType.Warning;
					}

					EditorGUILayout.HelpBox(text, type);
				}
			}
		}

		private bool Foldout(string key, string label)
		{
			_foldouts.TryGetValue(key, out var open);
			var newOpen = EditorGUILayout.Foldout(open, label, true);
			_foldouts[key] = newOpen;
			return newOpen;
		}
	}
}
