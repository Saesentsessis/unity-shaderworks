using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// The build's hook: strips the variants of every shader an <see cref="IShaderStrippingSource"/> describes, after the
	/// render pipeline's own stripping (so the logged totals are what actually ships). Other shaders are never touched.
	/// </summary>
	internal sealed class ShaderVariantStripper : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		/// <summary>
		/// After the render pipelines' preprocessors (URP's run at 0).
		/// </summary>
		public int callbackOrder => 1000;

		/// <summary>
		/// A new build: anything left from an earlier one (a failed or cancelled build) is ended first.
		/// </summary>
		public void OnPreprocessBuild(BuildReport report) => StrippingSession.End();

		public void OnPostprocessBuild(BuildReport report) => StrippingSession.End();

		public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
		{
			if (shader == null || data == null || data.Count == 0)
				return;

			StrippingSession.Current.Process(shader, snippet, data);
		}
	}
}
