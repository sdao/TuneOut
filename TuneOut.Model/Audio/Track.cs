using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;
using TuneOut.AppData;

namespace TuneOut.Audio
{
	/// <summary>
	/// Represents a single audio track. Immutable to clients.
	/// </summary>
	[DataContract(IsReference = true)]
	public class Track : ILibraryItem, IComparable<Track>, INotifyPropertyChanged
	{
		[DataMember(Name = "Artist")]
		private readonly string _Artist;

		[DataMember(Name = "ContainingAlbum")]
		private readonly Album _ContainingAlbum;

		[DataMember(Name = "DiscNumber")]
		private readonly int _DiscNumber;

		[DataMember(Name = "Location")]
		private readonly string _Location;

		[DataMember(Name = "Title")]
		private readonly string _Title;

		[DataMember(Name = "TotalTime")]
		private readonly TimeSpan _TotalTime;

		[DataMember(Name = "TrackID")]
		private readonly int _TrackID;

		[DataMember(Name = "TrackNumber")]
		private readonly int _TrackNumber;

		internal Track(int trackID, string title, string artist, int discNumber, int trackNumber, string location, TimeSpan totalTime, Album albumObj)
		{
			Contract.Requires(!string.IsNullOrEmpty(title));
			Contract.Requires(!string.IsNullOrEmpty(artist));
			Contract.Requires(!string.IsNullOrEmpty(location));
			Contract.Requires(albumObj != null);

			_TrackID = trackID;
			_Title = title;
			_Artist = artist;
			_DiscNumber = discNumber;
			_TrackNumber = trackNumber;
			_Location = location;
			_TotalTime = totalTime;
			_ContainingAlbum = albumObj;
		}
		/// <summary>
		/// Occurs when a property value changes.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Gets the name of the album.
		/// </summary>
		public string Album
		{
			get
			{
				return ContainingAlbum.Title;
			}
		}

		/// <summary>
		/// Gets the artist of the whole album.
		/// The album artist might be "Various Artists" or similar for compilations.
		/// </summary>
		public string AlbumArtist
		{
			get
			{
				return ContainingAlbum.AlbumArtist;
			}
		}

		/// <summary>
		/// Gets the individual track artist.
		/// </summary>
		public string Artist
		{
			get
			{
				return _Artist;
			}
		}

		/// <summary>
		/// Gets an artwork provider that determines the artwork of the track.
		/// </summary>
		public IArtworkProvider Artwork
		{
			get
			{
				return ContainingAlbum.Artwork;
			}
		}

		/// <summary>
		/// Gets or sets the Album object that contains the track.
		/// </summary>
		public Album ContainingAlbum
		{
			get
			{
				return _ContainingAlbum;
			}
		}

		/// <summary>
		/// Gets the same description for all Track objects.
		/// </summary>
		public string Description
		{
			get
			{
				return LocalizationManager.GetString("Items/Song/Description");
			}
		}

		/// <summary>
		/// Gets the number of the disc where this track appears.
		/// </summary>
		public int DiscNumber
		{
			get
			{
				return _DiscNumber;
			}
		}

		/// <summary>
		/// Gets whether the track is currently playing.
		/// </summary>
		public bool IsNowPlaying
		{
			get
			{
				return AudioController.Default.Current == this;
			}
		}

		/// <summary>
		/// Gets a file path to the formattedlocationString of the file on the system.
		/// </summary>
		public string Location
		{
			get
			{
				return _Location;
			}
		}

		/// <summary>
		/// Gets the subtitle of the track, comprised of its album, artist, and year, if available.
		/// </summary>
		public string Subtitle
		{
			get
			{
				if (Year > 0)
					return string.Format(LocalizationManager.GetString("Items/Song/Subtitle/Extended_F"), Album, Artist, Year);
				else
					return string.Format(LocalizationManager.GetString("Items/Song/Subtitle/Short_F"), Album, Artist);
			}
		}

		/// <summary>
		/// Gets the title of the track.
		/// </summary>
		public string Title
		{
			get
			{
				return _Title;
			}
		}

		/// <summary>
		/// Gets the total duration of the track.
		/// </summary>
		public TimeSpan TotalTime
		{
			get
			{
				return _TotalTime;
			}
		}

		/// <summary>
		/// Gets the order of this track on its disc.
		/// </summary>
		public int TrackNumber
		{
			get
			{
				return _TrackNumber;
			}
		}

		/// <summary>
		/// Gets the year of the track. If there is no year recorded, returns 0.
		/// </summary>
		public int Year
		{
			get
			{
				return ContainingAlbum.Year;
			}
		}

		/// <summary>
		/// Gets the track ID in the iTunes Library.
		/// </summary>
		internal int TrackID
		{
			get
			{
				return _TrackID;
			}
		}
		/// <summary>
		/// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
		/// </summary>
		/// <param name="other">An object to compare with this instance.</param>
		/// <returns>A value that indicates the relative order of the objects being compared.</returns>
		public int CompareTo(Track other)
		{
			if (other == null)
				return int.MaxValue;

			var discDiff = this.DiscNumber - other.DiscNumber;
			if (discDiff != 0)
				return discDiff;

			var trackDiff = this.TrackNumber - other.TrackNumber;
			if (trackDiff != 0)
				return trackDiff;

			return this.Title.CompareTo(other.Title);
		}

		/// <summary>
		/// Constructs a new <seealso cref="UniqueTrack"/> instance encapsulating this object.
		/// Use this method in situations where it is necessary to identify to separate references to the same
		/// <seealso cref="Track"/> instance, such as in a playlist where the same <seealso cref="Track"/>
		/// can occur twice.
		/// </summary>
		/// <returns>A new <seealso cref="UniqueTrack"/> instance with a completely new <seealso cref="System.Guid"/>.</returns>
		public UniqueTrack UniqueTrack()
		{
			return new UniqueTrack(this);
		}

		internal void NotifyNowPlaying()
		{
			OnPropertyChanged("IsNowPlaying");
		}
		/// <summary>
		/// Calls the <seealso cref="PropertyChanged"/> event handler if it is non-null.
		/// </summary>
		/// <param name="propertyName">The name of the property whose value has changed.</param>
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler h = PropertyChanged;
			if (h != null)
			{
				h(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(ContainingAlbum != null);
			Contract.Invariant(Title != null);
			Contract.Invariant(Artist != null);
			Contract.Invariant(Location != null);
		}
	}

	/// <summary>
	/// A structure that encapsulates a <seealso cref="Track"/> with an index.
	/// The index can be used to represent the index within a playlist,
	/// the actual track index within a disc, etc.
	/// </summary>
	public struct IndexedTrack
	{
		private readonly int _index;
		private readonly Track _track;

		/// <summary>
		/// Creates a new <seealso cref="IndexedTrack"/> with a specified track and index.
		/// </summary>
		/// <param name="index">The index of the track.</param>
		/// <param name="track">The encapsulated track.</param>
		public IndexedTrack(int index, Track track)
		{
			_index = index;
			_track = track;
		}

		/// <summary>
		/// Gets the index of the track.
		/// </summary>
		public int Index { get { return _index; } }
		/// <summary>
		/// Gets the encapsulated <seealso cref="Track"/> instance.
		/// </summary>
		public Track Track { get { return _track; } }
	}

	/// <summary>
	/// A structure that encapsulates a unique reference to a <seealso cref="Track"/> instance.
	/// Use this structure in situations where it is necessary to identify to separate references to the same
	/// <seealso cref="Track"/> instance, such as in a playlist where the same <seealso cref="Track"/>
	/// can occur twice.
	/// </summary>
	public struct UniqueTrack
	{
		/// <summary>
		/// The <seealso cref="UniqueTrack"/> that represents no track at all.
		/// </summary>
		public static readonly UniqueTrack Empty = new UniqueTrack();

		private readonly Track _track;
		private readonly Guid _uniqueId;

		/// <summary>
		/// Creates a new  <seealso cref="UniqueTrack"/> from a specified track.
		/// </summary>
		/// <param name="track">The track to encapsulate.
		/// If non-null, then the new <seealso cref="UniqueTrack"/> is guaranteed to be inequal to any other <seealso cref="UniqueTrack"/>.
		/// If null, then the new <seealso cref="UniqueTrack"/> is guaranteed to be equal to <seealso cref="UniqueTrack.Empty"/>.</param>
		public UniqueTrack(Track track)
		{
			if (track == null)
			{
				_uniqueId = Guid.Empty;
			}
			else
			{
				_uniqueId = Guid.NewGuid();
			}
			_track = track;
		}

		/// <summary>
		/// Gets the encapsulated <seealso cref="Track"/> instance.
		/// </summary>
		public Track Track { get { return _track; } }

		/// <summary>
		/// Gets the <seealso cref="System.Guid"/> that represents this specific encapsulation.
		/// </summary>
		public Guid UniqueId { get { return _uniqueId; } }
		/// <summary>
		/// Determines whether the specified object is equal to the current object.
		/// </summary>
		/// <param name="obj">The object to compare with the current object.</param>
		/// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
		public override bool Equals(object obj)
		{
			if (obj is UniqueTrack)
			{
				UniqueTrack t = (UniqueTrack)obj;
				return Track == t.Track && UniqueId == t.UniqueId;
			}
			else return false;
		}

		/// <summary>
		/// Serves as a hash function for a particular type..
		/// </summary>
		/// <returns>A hash code for the current <seealso cref="UniqueTrack"/>.</returns>
		public override int GetHashCode()
		{
			return _track == null ? 0 : 31 * (17 + _track.GetHashCode()) + _uniqueId.GetHashCode();
		}
	}
}