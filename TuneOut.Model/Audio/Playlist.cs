using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;
using TuneOut.AppData;

namespace TuneOut.Audio
{
	/// <summary>
	/// Represents a user-generated playlist of tracks.
	/// </summary>
	[DataContract]
	[KnownType(typeof(List<Track>))]
	public class Playlist : ITrackContainer
	{
		private static readonly StaticArtworkProvider PLAYLIST_ARTWORK_PROVIDER = new StaticArtworkProvider(Defaults.PlaylistArtwork);

		[DataMember(Name = "Title")]
		private readonly string _Title;

		[DataMember(Name = "TrackList")]
		private readonly IReadOnlyList<Track> _TrackList;

		/// <summary>
		/// Creates a Playlist with a given title and track list.
		/// </summary>
		/// <param name="name">The title of the playlist.</param>
		/// <param name="trackList">The track list, in order.</param>
		public Playlist(string name, List<Track> trackList)
		{
			Contract.Requires(trackList != null);
			Contract.Requires(!string.IsNullOrEmpty(name));

			_Title = name ?? LocalizationManager.GetString("Items/Playlist/DefaultTitle");
			_TrackList = trackList;
		}
		/// <summary>
		/// Gets the same image for all Playlist objects.
		/// </summary>
		public IArtworkProvider Artwork
		{
			get { return PLAYLIST_ARTWORK_PROVIDER; }
		}

		/// <summary>
		/// Gets the same description for all Album objects.
		/// </summary>
		public string Description
		{
			get
			{
				return LocalizationManager.GetString("Items/Playlist/Description");
			}
		}

		/// <summary>
		/// Gets the subtitle of the playlist, composed of the number of tracks in the playlist.
		/// </summary>
		public string Subtitle
		{
			get
			{
				if (TrackList.Count == 1)
				{
					return LocalizationManager.GetString("Items/Playlist/Subtitle/Single");
				}
				else if (TrackList.Count == 0)
				{
					return LocalizationManager.GetString("Items/Playlist/Subtitle/Zero");
				}
				else
				{
					return String.Format(LocalizationManager.GetString("Items/Playlist/Subtitle/Multiple_F"), TrackList.Count);
				}
			}
		}

		/// <summary>
		/// Gets the title of the playlist.
		/// </summary>
		public string Title
		{
			get
			{
				return _Title;
			}
		}
		/// <summary>
		/// Gets a list of the tracks in the playlist, in order.
		/// </summary>
		public IReadOnlyList<Track> TrackList
		{
			get
			{
				return _TrackList;
			}
		}

		/// <summary>
		/// Gets a collection of key-value pairs, where the key is the 1-based index of the track in the playlist, and where the value is the track.
		/// </summary>
		public IEnumerable<IndexedTrack> TrackListNumbered
		{
			get
			{
				return TrackList.Select((t, i) => new IndexedTrack(i + 1, t));
			}
		}
		/// <summary>
		/// Gets the index of a track within the container.
		/// </summary>
		/// <param name="t">The track.</param>
		/// <returns>A 0-based index, or -1 if not found.</returns>
		public int IndexOf(Track t)
		{
			int counter = 0;
			foreach (Track track in TrackList)
			{
				if (track == t)
				{
					return counter;
				}
				else
				{
					counter++;
				}
			}

			return -1;
		}

		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(TrackList != null);
			Contract.Invariant(Title != null);
		}
	}
}