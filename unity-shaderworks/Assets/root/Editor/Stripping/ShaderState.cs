using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// One schema's shader during a build: its rules (created with the shader's first snippet), the layout of each pass
	/// and stage, and what was kept.
	/// </summary>
	internal sealed class ShaderState
	{
		private readonly StrippingContext _context;
		private readonly Dictionary<(uint subshader, uint pass, ShaderType stage), Layout> _layouts = new();
		private readonly SortedDictionary<(string pass, string stage, string platform), VariantCounts> _snippets = new();
		private StrippingRules _rules;

		public ShaderState(IShaderStrippingSource source, ShaderStrippingSettings settings, ShaderKeywordSchema schema,
			StrippingContext context)
		{
			Source = source;
			Settings = settings;
			Schema = schema;
			_context = context;
		}

		public IShaderStrippingSource Source { get; }

		public ShaderStrippingSettings Settings { get; }

		public ShaderKeywordSchema Schema { get; }

		/// <summary>
		/// Totals over every snippet so far.
		/// </summary>
		public VariantCounts Counts { get; private set; }

		/// <summary>
		/// Strips <paramref name="data"/> in place: kept entries move forward in order, then the tail is cut once.
		/// </summary>
		public void Process(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data, StrippingSession session)
		{
			if (_rules == null)
			{
				_rules = StrippingRules.Create(Source, Settings, Settings.Mode, Schema, _context, shader);
				if (_rules.KeepsEverything)
					session.Warn($"{Source.DisplayName}: kept every variant of {Schema.ShaderName}: {_rules.KeepEverythingReason}.");
			}

			var count = data.Count;
			if (count == 0)
				return;

			if (_rules.KeepsEverything)
			{
				Record(snippet, data[0].shaderCompilerPlatform, count, count, 0);
				return;
			}

			var layout = GetLayout(shader, snippet);
			var write = 0;
			var unrecognized = 0;

			// Snippets normally hold one platform; counts are recorded whenever it changes.
			var platform = data[0].shaderCompilerPlatform;
			int runStart = 0, runWriteStart = 0, runUnrecognized = 0;

			for (var i = 0; i < count; i++)
			{
				var variant = data[i];
				if (variant.shaderCompilerPlatform != platform)
				{
					Record(snippet, platform, i - runStart, write - runWriteStart, runUnrecognized);
					platform = variant.shaderCompilerPlatform;
					runStart = i;
					runWriteStart = write;
					runUnrecognized = 0;
				}

				bool keep;
				if (layout.Pass.TryDecode(new KeywordSetQuery(variant.shaderKeywordSet, layout.Slots), out var bits))
				{
					keep = VariantMatching.IsKept(bits, _rules.Keep, layout.Pass.Exclusions);
				}
				else
				{
					keep = true;
					unrecognized++;
					runUnrecognized++;
				}

				if (keep)
					data[write++] = variant;
			}

			Record(snippet, platform, count - runStart, write - runWriteStart, runUnrecognized);

			if (data is List<ShaderCompilerData> list)
			{
				list.RemoveRange(write, count - write);
			}
			else
			{
				for (var i = count - 1; i >= write; i--)
					data.RemoveAt(i);
			}

			if (write == 0)
				session.Warn($"{Source.DisplayName}: every variant of {Schema.ShaderName}, pass '{snippet.passName}' ({snippet.shaderType}), was stripped. Check the Custom selection and the excluded combinations.");
		}

		public ShaderStrippingReport.ShaderEntry CreateReportEntry()
		{
			var entry = new ShaderStrippingReport.ShaderEntry
			{
				shader = Schema.ShaderName,
				mode = _rules?.Mode.ToString() ?? Settings.Mode.ToString(),
				keptEverythingBecause = _rules?.KeepEverythingReason ?? string.Empty,
				variants = Counts.Variants,
				kept = Counts.Kept,
				unrecognized = Counts.Unrecognized
			};

			if (_rules != null)
				entry.keptFor.AddRange(_rules.SourceNames);

			foreach (var pair in _snippets)
			{
				entry.snippets.Add(new ShaderStrippingReport.SnippetEntry
				{
					pass = pair.Key.pass,
					stage = pair.Key.stage,
					platform = pair.Key.platform,
					variants = pair.Value.Variants,
					kept = pair.Value.Kept,
					unrecognized = pair.Value.Unrecognized
				});
			}

			return entry;
		}

		private Layout GetLayout(Shader shader, in ShaderSnippetData snippet)
		{
			var pass = snippet.pass;
			var key = (pass.SubshaderIndex, pass.PassIndex, snippet.shaderType);
			if (_layouts.TryGetValue(key, out var layout))
				return layout;

			var keywords = ShaderUtil.GetPassKeywords(shader, pass, snippet.shaderType);
			var names = new HashSet<string>(System.StringComparer.Ordinal);
			foreach (var keyword in keywords)
				names.Add(keyword.name);

			var passLayout = PassLayout.Create(Schema, names);
			passLayout.ApplyExclusions(_rules.Exclusions);

			var slots = new LocalKeyword[passLayout.SlotKeywords.Count];
			for (var s = 0; s < slots.Length; s++)
			{
				foreach (var keyword in keywords)
				{
					if (keyword.name == passLayout.SlotKeywords[s])
					{
						slots[s] = keyword;
						break;
					}
				}
			}

			layout = new Layout(passLayout, slots);
			_layouts.Add(key, layout);
			return layout;
		}

		private void Record(in ShaderSnippetData snippet, ShaderCompilerPlatform platform, int variants, int kept, int unrecognized)
		{
			var counts = new VariantCounts(variants, kept, unrecognized);
			Counts += counts;

			var key = (snippet.passName, snippet.shaderType.ToString(), platform.ToString());
			_snippets.TryGetValue(key, out var snippetCounts);
			_snippets[key] = snippetCounts + counts;
		}

		private sealed class Layout
		{
			public readonly PassLayout Pass;
			public readonly LocalKeyword[] Slots;

			public Layout(PassLayout pass, LocalKeyword[] slots)
			{
				Pass = pass;
				Slots = slots;
			}
		}
	}
}
