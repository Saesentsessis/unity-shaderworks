using System.Collections.Generic;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// What a package tells the stripper: its shaders' keyword schemas, its settings, and what its features use.
	/// </summary>
	/// <remarks>
	/// Implement it in a package's editor assembly with a public parameterless constructor; the stripper finds every
	/// implementation (<c>TypeCache</c>) and creates one instance per build. Only the shaders of its
	/// <see cref="Schemas"/> are ever touched.
	/// </remarks>
	public interface IShaderStrippingSource
	{
		/// <summary>
		/// Stable identifier (report file names, logs), for example the package name.
		/// </summary>
		string Id { get; }

		/// <summary>
		/// Name shown in logs.
		/// </summary>
		string DisplayName { get; }

		IReadOnlyList<ShaderKeywordSchema> Schemas { get; }

		/// <summary>
		/// The package's current settings, read once per build.
		/// </summary>
		ShaderStrippingSettings Settings { get; }

		/// <summary>
		/// <see cref="StrippingMode.Automatic"/>: adds one keep pattern per setup that draws with the schema's shader (for
		/// example one per renderer feature) to <paramref name="patterns"/>. Adding none keeps every variant (a source
		/// that sees nothing never strips).
		/// </summary>
		/// <param name="sourceNames">Names of what produced the patterns (for example renderer features), for logs.</param>
		void CollectAutomaticPatterns(StrippingContext context, ShaderKeywordSchema schema, List<VariantBits> patterns,
			List<string> sourceNames);
	}
}
