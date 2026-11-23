using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// What a build uses, handed to <see cref="IShaderStrippingSource.CollectAutomaticPatterns"/>.
	/// </summary>
	public sealed class StrippingContext
	{
		/// <summary>
		/// The render pipeline assets a build for <paramref name="target"/> uses: those of the Quality levels active for
		/// the target, and the Graphics settings' default one when a level has none (or no level is active).
		/// </summary>
		public static List<RenderPipelineAsset> GetPipelineAssets(BuildTarget target)
		{
			var assets = new List<RenderPipelineAsset>();
			var platform = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target)).TargetName;

#if UNITY_6000_0_OR_NEWER
			QualitySettings.GetRenderPipelineAssetsForPlatform<RenderPipelineAsset>(platform, out var qualityAssets,
				out var allLevelsOverridden);

			foreach (var asset in qualityAssets)
			{
				if (asset != null && assets.Contains(asset) == false)
					assets.Add(asset);
			}

			var noLevels = QualitySettings.GetActiveQualityLevelsForPlatformCount(platform) == 0;
#else
			// 2022.3 has no overload that reports whether every level has an asset: walk the levels.
			var levels = QualitySettings.GetActiveQualityLevelsForPlatform(platform);
			var allLevelsOverridden = true;
			foreach (var level in levels)
			{
				var asset = QualitySettings.GetRenderPipelineAssetAt(level);
				if (asset == null)
					allLevelsOverridden = false;
				else if (assets.Contains(asset) == false)
					assets.Add(asset);
			}

			var noLevels = levels.Length == 0;
#endif
			var fallback = GraphicsSettings.defaultRenderPipeline;
			if ((noLevels || allLevelsOverridden == false) && fallback != null && assets.Contains(fallback) == false)
				assets.Add(fallback);

			// Deterministic order, so logs and reports don't change between identical builds.
			assets.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
			return assets;
		}

		public StrippingContext(BuildTarget target, IReadOnlyList<RenderPipelineAsset> pipelineAssets, bool countInactiveSources)
		{
			Target = target;
			PipelineAssets = pipelineAssets;
			CountInactiveSources = countInactiveSources;
		}

		public BuildTarget Target { get; }

		/// <summary>
		/// The render pipeline assets the build uses (see <see cref="GetPipelineAssets"/>).
		/// </summary>
		public IReadOnlyList<RenderPipelineAsset> PipelineAssets { get; }

		/// <summary>
		/// Whether features turned off on their renderer still count as used.
		/// </summary>
		public bool CountInactiveSources { get; }
	}
}
