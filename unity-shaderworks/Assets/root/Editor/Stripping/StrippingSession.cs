using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace Saesentsessis.Shaders.Stripping
{
#if UNITY_6000_5_OR_NEWER
	// Build-time state: created by the first shader of a build, ended (and cleared) when the build ends.
	[NoAutoStaticsCleanup]
#endif
	/// <summary>
	/// The stripping state of one build: the sources, their rules per shader, and what was kept.
	/// </summary>
	/// <remarks>
	/// Created by the first shader the build processes, never carried over: player builds end it from their post-build
	/// callback; asset bundle builds and failed builds (which call none) end it on the Editor's next update, since a
	/// build blocks the main thread until it finishes.
	/// </remarks>
	internal sealed class StrippingSession
	{
		private const string LogPrefix = "[Shader Stripping] ";
		private const string ReportDirectory = "Logs/ShaderStripping";

		private static StrippingSession _current;

		public static StrippingSession Current
		{
			get
			{
				if (_current == null)
				{
					_current = new StrippingSession(EditorUserBuildSettings.activeBuildTarget);
					EditorApplication.delayCall += End;
				}

				return _current;
			}
		}

		/// <summary>
		/// Ends the current session, if any: logs its summaries and writes its reports.
		/// </summary>
		public static void End()
		{
			var session = _current;
			_current = null;
			EditorApplication.delayCall -= End;
			session?.Flush();
		}

		/// <summary>
		/// Every source in the project, in a stable order (by type name); a source that can't be created is logged and
		/// skipped.
		/// </summary>
		public static List<IShaderStrippingSource> CreateSources()
		{
			var types = new List<Type>();
			foreach (var type in TypeCache.GetTypesDerivedFrom<IShaderStrippingSource>())
			{
				if (type.IsAbstract == false && type.IsInterface == false && type.ContainsGenericParameters == false
				    && type.GetConstructor(Type.EmptyTypes) != null)
					types.Add(type);
			}

			types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

			var sources = new List<IShaderStrippingSource>(types.Count);
			foreach (var type in types)
			{
				try
				{
					sources.Add((IShaderStrippingSource)Activator.CreateInstance(type));
				}
				catch (Exception exception)
				{
					Debug.LogError($"{LogPrefix}Couldn't create {type.FullName}; its shaders are not stripped.\n{exception}");
				}
			}

			return sources;
		}

		private static string FormatSummary(ShaderState state, ShaderStrippingReport.ShaderEntry entry)
		{
			var text = new StringBuilder(LogPrefix)
				.Append(state.Source.DisplayName).Append(": kept ").Append(entry.kept).Append(" of ").Append(entry.variants)
				.Append(" variants of ").Append(entry.shader).Append(" (").Append(entry.mode);

			if (entry.keptFor.Count > 0)
				text.Append("; for ").Append(string.Join(", ", entry.keptFor));

			text.Append(')');

			if (string.IsNullOrEmpty(entry.keptEverythingBecause) == false)
				text.Append(". Nothing stripped: ").Append(entry.keptEverythingBecause).Append('.');

			if (entry.unrecognized > 0)
				text.Append(". ").Append(entry.unrecognized).Append(" variants didn't fit the schema and were kept.");

			return text.ToString();
		}

		private readonly BuildTarget _target;
		private readonly Dictionary<string, ShaderState> _shadersByName = new(StringComparer.Ordinal);
		private readonly Dictionary<Shader, ShaderState> _shaders = new();
		private readonly HashSet<string> _warnings = new(StringComparer.Ordinal);

		private StrippingSession(BuildTarget target)
		{
			_target = target;
			var pipelineAssets = StrippingContext.GetPipelineAssets(target);

			foreach (var source in CreateSources())
			{
				ShaderStrippingSettings settings;
				try
				{
					settings = source.Settings ?? new ShaderStrippingSettings();
				}
				catch (Exception exception)
				{
					Debug.LogError($"{LogPrefix}{source.GetType().FullName} has no readable settings; its shaders are not stripped.\n{exception}");
					continue;
				}

				var context = new StrippingContext(target, pipelineAssets, settings.CountInactiveSources);
				foreach (var schema in source.Schemas)
				{
					if (_shadersByName.TryGetValue(schema.ShaderName, out var other))
					{
						Warn($"{source.DisplayName} and {other.Source.DisplayName} both describe shader '{schema.ShaderName}'; only {other.Source.DisplayName}'s rules apply.");
						continue;
					}

					_shadersByName.Add(schema.ShaderName, new ShaderState(source, settings, schema, context));
				}
			}
		}

		/// <summary>
		/// Strips <paramref name="data"/> in place if <paramref name="shader"/> belongs to a source.
		/// </summary>
		public void Process(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
		{
			if (_shaders.TryGetValue(shader, out var state) == false)
			{
				// Shader names are looked up once per shader and build (Shader.name allocates).
				_shadersByName.TryGetValue(shader.name, out state);
				_shaders.Add(shader, state);
			}

			if (state == null)
				return;

			state.Process(shader, snippet, data, this);
		}

		public void Warn(string message)
		{
			if (_warnings.Add(message))
				Debug.LogWarning(LogPrefix + message);
		}

		private void Flush()
		{
			var reports = new SortedDictionary<string, ShaderStrippingReport>(StringComparer.Ordinal);
			var states = new List<ShaderState>(_shadersByName.Values);
			states.Sort((a, b) => string.CompareOrdinal(a.Schema.ShaderName, b.Schema.ShaderName));

			foreach (var state in states)
			{
				if (state.Counts.Variants == 0)
					continue;

				var entry = state.CreateReportEntry();
				if (state.Settings.LogSummary)
					Debug.Log(FormatSummary(state, entry));

				if (state.Settings.WriteReport == false)
					continue;

				if (reports.TryGetValue(state.Source.Id, out var report) == false)
				{
					report = new ShaderStrippingReport
					{
						source = state.Source.Id,
						buildTarget = _target.ToString(),
						time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
					};
					reports.Add(state.Source.Id, report);
				}

				report.shaders.Add(entry);
			}

			foreach (var report in reports.Values)
				WriteReport(report);
		}

		private void WriteReport(ShaderStrippingReport report)
		{
			try
			{
				Directory.CreateDirectory(ReportDirectory);
				var fileName = string.Join("_", report.source.Split(Path.GetInvalidFileNameChars())) + ".json";
				File.WriteAllText(Path.Combine(ReportDirectory, fileName), JsonUtility.ToJson(report, true));
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"{LogPrefix}Couldn't write the report of {report.source}: {exception.Message}");
			}
		}
	}
}
