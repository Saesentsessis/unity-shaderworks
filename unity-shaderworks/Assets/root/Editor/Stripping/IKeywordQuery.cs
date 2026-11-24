namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// Whether each keyword slot of a <see cref="PassLayout"/> is on in one variant. Implemented by structs, so decoding
	/// allocates nothing and needs no delegate.
	/// </summary>
	internal interface IKeywordQuery
	{
		bool IsEnabled(int slot);
	}
}
