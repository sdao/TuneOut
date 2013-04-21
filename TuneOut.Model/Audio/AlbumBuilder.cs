using TuneOut.AppData;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuneOut.Audio
{
    /// <summary>
    /// Constructs Album and Track objects within these albums.
    /// </summary>
    public class AlbumBuilder
    {
        readonly List<Track> _tracks = new List<Track>();
        readonly Album _album;
        
        /// <summary>
        /// Creates a new AlbumBuilder.
        /// </summary>
        public AlbumBuilder(string title, string albumArtist, int year)
        {
            Valid = true;
            _album = new Album(_tracks,
                title ?? LocalizationManager.GetString("Items/Song/DefaultAlbum", "Untitled"),
                albumArtist ?? LocalizationManager.GetString("Items/Song/DefaultArtist", "Unknown"),
                year);
        }

        /// <summary>
        /// Adds a Track to the curren Album in construction.  Valid must be true, or an <seealso cref="System.InvalidOperationException"/> will be thrown.
        /// </summary>
        /// <param name="trackID">The track ID.</param>
        /// <param name="title">The song title.</param>
        /// <param name="artist">The song artist.</param>
        /// <param name="discNumber">The disc number.</param>
        /// <param name="trackNumber">The track number.</param>
        /// <param name="location">The file location on disk.</param>
        /// <param name="totalTime">The total running time.</param>
        public void AddTrack(int trackID, string title, string artist, int discNumber, int trackNumber, string location, TimeSpan totalTime)
        {
            Contract.Requires(location != null);
            Contract.Requires<InvalidOperationException>(Valid);

            Track t = new Track(
                trackID: trackID,
                title: title ?? LocalizationManager.GetString("Items/Song/DefaultTitle", "Untitled"),
                artist: artist ?? LocalizationManager.GetString("Items/Song/DefaultArtist", "Unknown"),
                discNumber: discNumber,
                trackNumber: trackNumber,
                location: location,
                totalTime: totalTime,
                albumObj: _album);

            int insertionIndex = _tracks.BinarySearch(t);
            if (insertionIndex < 0)
            {
                insertionIndex = ~insertionIndex;
            }
            _tracks.Insert(insertionIndex, t);
        }

        /// <summary>
        /// Determines whether the AlbumBuilder can be used.
        /// If Valid is true, the AlbumBuilder can be used.
        /// If Valid is false, the AlbumBuilder is complete, and can no longer be used.
        /// </summary>
        public bool Valid
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the Album product.
        /// After getting the product, Valid becomes false, and no further products are authorized.
        /// </summary>
        /// <returns>The Album.</returns>
        public Album GetAlbum()
        {
            Valid = false;
            return _album;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_tracks != null);
            Contract.Invariant(_album != null);
        }
    }
}
