namespace TuneOut.Audio
{
	/// <summary>
	/// Defines an item that can be searched and indexed as part of the music library.
	/// </summary>
	public interface ILibraryItem
	{
		/// <summary>
		/// Gets an artwork provider that determines the artwork of the object.
		/// </summary>
		IArtworkProvider Artwork
		{
			get;
		}

		/// <summary>
		/// Gets the kind of a single object, such as Album, Track, or Playlist.
		/// </summary>
		string Description
		{
			get;
		}

		/// <summary>
		/// Gets secondary identifying characteristics of the object, such as artist or year.
		/// </summary>
		string Subtitle
		{
			get;
		}

		/// <summary>
		/// Gets the title or name of the object.
		/// </summary>
		string Title
		{
			get;
		}
	}
}