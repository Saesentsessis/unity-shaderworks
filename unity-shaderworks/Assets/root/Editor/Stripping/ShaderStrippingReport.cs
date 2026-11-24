using System;
using System.Collections.Generic;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// One build's stripping for one source, as written to <c>Logs/ShaderStripping/&lt;source id&gt;.json</c>. Entries are
	/// sorted, so reports of two builds diff cleanly.
	/// </summary>
	[Serializable]
	internal sealed class ShaderStrippingReport
	{
		public string source;
		public string buildTarget;
		public string time;
		public List<ShaderEntry> shaders = new();

		[Serializable]
		public sealed class ShaderEntry
		{
			public string shader;
			public string mode;
			public List<string> keptFor = new();

			/// <summary>
			/// Why nothing was stripped, or empty.
			/// </summary>
			public string keptEverythingBecause;

			public long variants;
			public long kept;
			public long unrecognized;
			public List<SnippetEntry> snippets = new();
		}

		[Serializable]
		public sealed class SnippetEntry
		{
			public string pass;
			public string stage;
			public string platform;
			public long variants;
			public long kept;
			public long unrecognized;
		}
	}
}
