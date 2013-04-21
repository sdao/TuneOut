using System.Collections.Generic;

namespace TuneOut.Audio
{
    /// <summary>
    /// A library item that can contain Tracks.
    /// </summary>
    public interface ITrackContainer : ILibraryItem
    {
        /// <summary>
        /// Gets a list of the tracks in the container, in order.
        /// </summary>
        IReadOnlyList<Track> TrackList
        {
            get;
        }

        /// <summary>
        /// Gets the index of a track within the container.
        /// </summary>
        /// <param name="t">The track.</param>
        /// <returns>A 0-based index, or -1 if not found.</returns>
        int IndexOf(Track t);

        /// <summary>
        /// Gets a collection of key-value pairs, where the key is the labelled number of the track (but not necessarily the index), and where the value is the track.
        /// </summary>
        IEnumerable<IndexedTrack> TrackListNumbered
        {
            get;
        }
    }
}
