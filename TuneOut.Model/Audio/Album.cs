using TuneOut.AppData;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;

namespace TuneOut.Audio
{
    /// <summary>
    /// Represents an album, record, or compilation. Immutable to clients.
    /// </summary>
    [DataContract(IsReference=true)]
    [KnownType(typeof(List<Track>))]
    public class Album : ITrackContainer
    {
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

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Artwork = new LastFmArtworkProvider(this);
        }

        [DataMember(Name="Title")]
        readonly string _Title;
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
        /// Gets the same description for all Album objects.
        /// </summary>
        public string Description
        {
            get
            {
                return LocalizationManager.GetString("Items/Album/Description");
            }
        }

        [DataMember(Name="AlbumArtist")]
        readonly string _AlbumArtist;
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

        [DataMember(Name = "Year")]
        readonly int _Year;
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
        /// Gets an artwork provider that determines the artwork of the album.
        /// </summary>
        public IArtworkProvider Artwork
        {
            get;
            private set;
        }

        [DataMember(Name="TrackList")]
        readonly IReadOnlyList<Track> _TrackList;
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

        [DataMember(Name="AlbumID")]
        readonly uint _AlbumID;
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
    }

    internal struct UniqueAlbum
    {
        readonly string _albumString;
        readonly string _albumArtistString;

        public UniqueAlbum(string albumString, string albumArtistString)
        {
            Contract.Requires(albumString != null);
            Contract.Requires(albumArtistString != null);

            _albumString = albumString;
            _albumArtistString = albumArtistString;
        }

        public override bool Equals(object obj)
        {
            if (obj is UniqueAlbum)
            {
                UniqueAlbum a = (UniqueAlbum)obj;
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
