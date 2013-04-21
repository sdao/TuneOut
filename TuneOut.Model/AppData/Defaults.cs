using System;

namespace TuneOut.AppData
{
    /// <summary>
    /// Manages default strings, paths, and URIs that should be provided by the client.
    /// </summary>
    public static class Defaults
    {
        /// <summary>
        /// Gets or sets the <seealso cref="Uri"/> that points to the image file for unknown library items.
        /// </summary>
        public static Uri UnknownArtwork
        {
            get;
            set;
        }

        /// <summary>
        /// Gets and sets the <seealso cref="Uri"/> that points to the image file for playlists.
        /// </summary>
        public static Uri PlaylistArtwork
        {
            get;
            set;
        }
    }
}
