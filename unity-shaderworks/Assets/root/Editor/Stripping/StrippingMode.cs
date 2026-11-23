namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// How a package's shader variants are chosen for player builds.
	/// </summary>
	public enum StrippingMode
	{
		/// <summary>
		/// Keep what the build's render pipeline assets use (the package decides from its renderer features), plus the
		/// variants of the listed shader variant collections.
		/// </summary>
		Automatic = 0,

		/// <summary>
		/// Keep every combination of the values ticked per keyword group.
		/// </summary>
		Custom = 1
	}
}
