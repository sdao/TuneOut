using TuneOut.AppData;
using System;

namespace TuneOut.Audio
{
    class StaticArtworkProvider : IArtworkProvider
    {
        public static readonly StaticArtworkProvider UNKNOWN = new StaticArtworkProvider(Defaults.UnknownArtwork);

        public StaticArtworkProvider(Uri artUri)
        {
            _Image = artUri;
        }

        readonly Uri _Image = null;
        public Uri Image
        {
            get { return _Image; }
        }
    }
}
