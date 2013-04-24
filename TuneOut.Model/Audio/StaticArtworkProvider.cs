using System;
using TuneOut.AppData;

namespace TuneOut.Audio
{
	internal class StaticArtworkProvider : IArtworkProvider
	{
		private readonly Uri _Image = null;

		public StaticArtworkProvider(Uri artUri)
		{
			_Image = artUri;
		}
		public Uri Image
		{
			get { return _Image; }
		}
	}
}