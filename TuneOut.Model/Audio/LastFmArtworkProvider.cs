using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TuneOut.AppData;
using Windows.Data.Json;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;

namespace TuneOut.Audio
{
	internal class LastFmArtworkProvider : IArtworkProvider, INotifyPropertyChanged
	{
		private const int DOWNLOAD_THROTTLE_RATE = 10;

		private static readonly HttpClient __client = new HttpClient();
		private static readonly BackgroundDownloader __downloader = new BackgroundDownloader();
		private static readonly SemaphoreSlim __semaphore = new SemaphoreSlim(DOWNLOAD_THROTTLE_RATE);

		private readonly Album _album;

		private Uri _cachedUri;
		private bool _queried;

		static LastFmArtworkProvider()
		{
			Settings.CreateArtworkCache();
			Settings.CleanArtworkCache();
			__client.DefaultRequestHeaders.Add("user-agent", LastFmApiSecrets.LASTFMAPI_USERAGENT);
		}

		public LastFmArtworkProvider(Album album)
		{
			Contract.Requires(album != null);
			_album = album;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public Uri Image
		{
			get
			{
				if (!_queried)
				{
					_queried = true;

					var data = Settings.GetArtworkCacheItem(_album.AlbumID);
					if (data.Status == CacheStatus.Cached)
					{
						// Cached, do not attempt to queue.
						_cachedUri = data.CachedObject;
					}
					else if (data.Status == CacheStatus.Uncached && NetworkStatusManager.Default.IsUnlimitedInternetAvailable)
					{
						// Uncached, do work.
						Query();
					}
				}

				return _cachedUri ?? Defaults.UnknownArtwork;
			}

			private set
			{
				_cachedUri = value;
				OnPropertyChanged("Image");
			}
		}

		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler h = PropertyChanged;
			if (h != null)
			{
				h(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		/// <summary>
		/// Asynchronously download the artwork for an album from Last.fm.
		/// </summary>
		/// <param name="artGuid">The current Guid for the art container.</param>
		/// <returns>A CacheToken that indicates whether the cache succeeded, and the Uri to the image if so.</returns>
		private async Task<CacheToken<Guid, Uri>> DownloadAsync(Guid artGuid)
		{
			// Download art from Last.fm
			try
			{
				HttpResponseMessage response = await __client.GetAsync(String.Format(LastFmApiSecrets.LASTFMAPI_ALBUMGETINFO, Uri.EscapeDataString(_album.AlbumArtist), Uri.EscapeDataString(_album.Title)));
				string responseText = await response.Content.ReadAsStringAsync();

				string imageLoc = JsonObject
					.Parse(responseText)
					.GetNamedObject("album")
					.GetNamedArray("image")[3]
					.GetObject()
					.GetNamedString("#text");

				Uri source = new Uri(imageLoc);
				StorageFolder destinationFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(artGuid.ToString(), CreationCollisionOption.OpenIfExists);
				StorageFile destinationFile = await destinationFolder.CreateFileAsync(_album.AlbumID.ToString(), CreationCollisionOption.ReplaceExisting);

				DownloadOperation download = __downloader.CreateDownload(source, destinationFile);
				await download.StartAsync();
			}
			catch (HttpRequestException)
			{
				return new CacheToken<Guid, Uri>(CacheStatus.Uncached, artGuid, null);
			}
			catch (WebException)
			{
				return new CacheToken<Guid, Uri>(CacheStatus.Uncached, artGuid, null);
			}
			catch (Exception)
			{
				return new CacheToken<Guid, Uri>(CacheStatus.CannotCache, artGuid, null);
			}

			return new CacheToken<Guid, Uri>(CacheStatus.Cached, artGuid, new Uri(String.Format("ms-appdata:///local/{0}/{1}", artGuid.ToString(), _album.AlbumID)));
		}

		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(_album != null);
		}

		/// <summary>
		/// Query for album art online.
		/// </summary>
		private async void Query()
		{
			await __semaphore.WaitAsync();
			var results = await Task.Run(() => DownloadAsync(Settings.ArtContainerGuid));
			__semaphore.Release();

			switch (results.Status)
			{
				case CacheStatus.Uncached:
					// Web failure
					break;

				case CacheStatus.CannotCache:
					// Known: no data
					Settings.SetArtworkCacheItem(results.CachedObject1, CacheStatus.CannotCache, _album.AlbumID, null);
					break;

				case CacheStatus.Cached:
					// Got album art!
					if (Settings.SetArtworkCacheItem(results.CachedObject1, CacheStatus.Cached, _album.AlbumID, results.CachedObject2))
					{
						// Only save if the artwork was saved successfully; otherwise, a reset occurred!
						Image = results.CachedObject2;
					}
					break;
			}
		}
	}
}