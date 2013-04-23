using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;
using TuneOut.AppData;

namespace TuneOut.Audio
{
	/// <summary>
	/// Represents an album, record, or compilation. Immutable to clients.
	/// </summary>
	[DataContract(IsReference = true)]
	[KnownType(typeof(List<Track>))]
	public class Album : ITrackContainer
	{
		[DataMember(Name = "AlbumArtist")]
		private readonly string _AlbumArtist;

		[DataMember(Name = "AlbumID")]
		private readonly uint _AlbumID;

		[DataMember(Name = "Title")]
		private readonly string _Title;

		[DataMember(Name = "TrackList")]
		private readonly IReadOnlyList<Track> _TrackList;

		[DataMember(Name = "Year")]
		private readonly int _Year;

		internal Album(List<Track> trackList, string title, string albumArtist, int year)
		{
			Contract.Requires(trackList != null);
			Contract.Requires(!string.IsNullOrEmpty(title));
			Contract.Requires(!string.IsNullOrEmpty(albumArtist));

			_TrackList = trackList;
			_Title = title;
			_AlbumArtist = albumArtist;
			_Year = year;
			_AlbumID = (uint)(31 * Title.GetHashCode() + AlbumArtist.GetHashCode());
			Artwork = new LastFmArtworkProvider(this);
		}

		/// <summary>
		/// Gets the album artist from the first track.
		/// </summary>
		public string AlbumArtist
		{
			get
			{
				return _AlbumArtist;
			}
		}

		/// <summary>
		/// Gets an artwork provider that determines the artwork of the album.
		/// </summary>
		public IArtworkProvider Artwork
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the same description for all Album objects.
		/// </summary>
		public string Description
		{
			get
			{
				return LocalizationManager.GetString("Items/Album/Description");
			}
		}

		/// <summary>
		/// Gets the subtitle of the album, composed of the album artist and the year, if available.
		/// </summary>
		public string Subtitle
		{
			get
			{
				return Year > 0 ? String.Format(LocalizationManager.GetString("Items/Album/Subtitle_F"), AlbumArtist, Year) : AlbumArtist;
			}
		}

		/// <summary>
		/// Gets the title of the album from the first track.
		/// </summary>
		public string Title
		{
			get
			{
				return _Title;
			}
		}

		/// <summary>
		/// Gets a list of the tracks in the album, in order.
		/// </summary>
		public IReadOnlyList<Track> TrackList
		{
			get
			{
				return _TrackList;
			}
		}

		/// <summary>
		/// Gets a collection of key-value pairs, where the key is the track number (and not the index), and where the value is the track.
		/// </summary>
		public IEnumerable<IndexedTrack> TrackListNumbered
		{
			get
			{
				return TrackList.Select((t) => new IndexedTrack(t.TrackNumber, t));
			}
		}

		/// <summary>
		/// Gets the year of the album from the first track. If there is no year recorded, returns 0.
		/// </summary>
		public int Year
		{
			get
			{
				return _Year;
			}
		}

		/// <summary>
		/// Gets a unique Album ID. Implementation details subject to change.
		/// </summary>
		internal uint AlbumID
		{
			get
			{
				return _AlbumID;
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
			Contract.Invariant(AlbumArtist != null);
		}

		[OnDeserialized]
		private void OnDeserialized(StreamingContext context)
		{
			Artwork = new LastFmArtworkProvider(this);
		}
	}

	/// <summary>
	/// A unique key that can be used for <seealso cref="Album"/> objects in a dictionary.
	/// </summary>
	internal struct AlbumKey
	{
		private readonly string _albumArtistString;
		private readonly string _albumString;
		public AlbumKey(string albumString, string albumArtistString)
		{
			Contract.Requires(albumString != null);
			Contract.Requires(albumArtistString != null);

			_albumString = albumString;
			_albumArtistString = albumArtistString;
		}

		public override bool Equals(object obj)
		{
			if (obj is AlbumKey)
			{
				AlbumKey a = (AlbumKey)obj;
				return _albumString == a._albumString && _albumArtistString == a._albumArtistString;
			}
			else return false;
		}

		public override int GetHashCode()
		{
			return 31 * (17 + _albumString.GetHashCode()) + _albumArtistString.GetHashCode();
		}

		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(_albumString != null);
			Contract.Invariant(_albumArtistString != null);
		}
	}
}