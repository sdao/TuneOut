using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using TuneOut.AppData;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace TuneOut.Audio
{
	/// <summary>
	/// A helper for accessing the data in the iTunes Library.
	/// </summary>
	[DataContract]
	[KnownType(typeof(IEnumerable<Album>))]
	[KnownType(typeof(IEnumerable<Playlist>))]
	public class TunesDataSource
	{
		private const string ITUNES_CACHE_XML = "_itml.xml";
		private const string ITUNES_XML = "iTunes Music Library.xml";

		#region Static members

		/// <summary>
		/// Gets the default singleton instance, if it has been loaded.
		/// </summary>
		public static TunesDataSource Default
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets whether a TunesDataSource is available.
		/// </summary>
		public static bool IsLoaded
		{
			get
			{
				return TunesDataSource.Default != null;
			}
		}

		/// <summary>
		/// Initializes the default TunesDataSource in silent mode. Any error will automatically return false.
		/// When completed, Default will be available.
		/// </summary>
		/// <returns>An async Task with the initialization status. If false, initialization failed.</returns>
		public static async Task<bool> Load()
		{
			if (TunesDataSource.Default != null)
			{
				return true;
			}

			try
			{
				// Attempt to find the iTunes Library first
				if (Settings.GetLibraryLocationStatus())
				{
					var iTunesLibLocation = await Settings.GetLibraryLocation();
					return (await LoadLibraryFromCache(iTunesLibLocation) || await LoadLibraryFromXml(iTunesLibLocation));
				}

				return false;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Resets the default TunesDataSource and unlinks any remembered iTunes Library files.
		/// </summary>
		public static void Reset()
		{
			Settings.ResetArtworkCache();
			Settings.LastLibraryUpdate = DateTimeOffset.MinValue;
			Settings.IsFirstRunComplete = false;
			Settings.SetLibraryLocation(null);

			TunesDataSource.Default = null;
		}

		private static async Task<bool> DeserializeAsync()
		{
			StorageFile file = null;
			TunesDataSource deserializedSource = null;

			try
			{
				file = await ApplicationData.Current.LocalFolder.GetFileAsync(ITUNES_CACHE_XML);

				using (IInputStream stream = await file.OpenReadAsync())
				{
					DataContractSerializer dcs = new DataContractSerializer(typeof(TunesDataSource));
					deserializedSource = (TunesDataSource)dcs.ReadObject(stream.AsStreamForRead());
				}
			}
			catch (Exception) { }

			if (deserializedSource != null)
			{
				TunesDataSource.Default = deserializedSource;
				return true;
			}
			else
			{
				return false;
			}
		}

		private static string GetAbsoluteMediaPathUri(string actualLibraryPath, string declaredMediaPath)
		{
			Contract.Requires(!string.IsNullOrEmpty(actualLibraryPath));
			Contract.Requires(!string.IsNullOrEmpty(declaredMediaPath));

			declaredMediaPath = declaredMediaPath.Trim('\\');
			actualLibraryPath = actualLibraryPath.Trim('\\');

			string[] actualPathComps = actualLibraryPath.Split('\\'); // Hope that harcoding slash does not do horrible things.
			string[] declaredPathComps = declaredMediaPath.Split('\\');

			if (actualPathComps.Length > 0)
			{
				for (int i = declaredPathComps.Length - 1; i >= 0; i--)
				{
					// The Media path should be within the Library path
					if (declaredPathComps[i] == actualPathComps[actualPathComps.Length - 1])
					{
						System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();

						for (int j = 0; j < actualPathComps.Length; j++)
						{
							stringBuilder.Append(actualPathComps[j]);
							stringBuilder.Append('\\');
						}

						for (int j = i + 1; j < declaredPathComps.Length; j++)
						{
							stringBuilder.Append(declaredPathComps[j]);
							stringBuilder.Append('\\');
						}

						return stringBuilder.ToString();
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Determines whether a file can be played by looking at its file path and bitrate.
		/// Unknown file extensions are unplayable.
		/// Files with a high bitrate are assumed to be Apple Lossless and are unplayable.
		/// </summary>
		/// <param name="path">The file path.</param>
		/// <param name="bitRate">The bit rate.</param>
		/// <returns>Whether the file can be played.</returns>
		private static bool IsValidMediaKind(string path, long bitRate)
		{
			if (path == null)
				return false;

			var capS = path.ToUpperInvariant();

			if ((capS.EndsWith(".MP3") || capS.EndsWith(".M4A") || capS.EndsWith(".MP4"))
				&& bitRate < 500)
				return true;
			else
				return false;
		}

		/// <summary>
		/// Loads the iTunes Library from a cache if the cache is available and has not yet expired.
		/// </summary>
		/// <param name="library">The folder containing the iTunes Library.</param>
		/// <returns>Whether the process was successful or not.</returns>
		private static async Task<bool> LoadLibraryFromCache(StorageFolder library)
		{
			bool cachedAfterLastUpdate = false;
			bool cacheRestoreStatus = false;

			// Check XML file timestamp
			StorageFile xmlFile = await library.GetFileAsync(ITUNES_XML);
			BasicProperties xmlFileProperties = await xmlFile.GetBasicPropertiesAsync();
			DateTimeOffset xmlFileCurrentDate = xmlFileProperties.DateModified;

			// Check cached time
			var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
			if (Settings.LastLibraryUpdate > xmlFileCurrentDate)
				cachedAfterLastUpdate = true;

			// If possible, load the cache!
			if (cachedAfterLastUpdate)
				cacheRestoreStatus = await DeserializeAsync();

			return cacheRestoreStatus;
		}

		/// <summary>
		/// Loads the iTunes Library from an XML file and reads the PLIST-formatted contents.
		/// </summary>
		/// <param name="library">The folder containing the iTunes Library.</param>
		/// <returns>Whether the process was successful or not.</returns>
		private static async Task<bool> LoadLibraryFromXml(StorageFolder library)
		{
			StorageFile xmlFile = await library.GetFileAsync(ITUNES_XML);
			XDocument xmlDocument = null;

			// Attempt to read the XML file format
			var xmlStreamInfo = await xmlFile.TryReadAsync();
			if (xmlStreamInfo.Item1.HasValue)
			{
				OperatingSystem libraryType = xmlStreamInfo.Item1.Value;
				using (var xmlStream = xmlStreamInfo.Item2)
				{
					xmlDocument = XDocument.Load(xmlStream.AsStreamForRead());
				}

				// Get the PLIST from the XML and parse
				var plist = new PList(xmlDocument);

				IEnumerable<Album> libraryAlbums;
				IEnumerable<Playlist> libraryPlaylists;
				var parseResult = ParseXml(library.Path, plist, out libraryAlbums, out libraryPlaylists);

				// Save the parsed objects if possible
				if (parseResult)
				{
					TunesDataSource.Default = new TunesDataSource(libraryAlbums, libraryPlaylists, libraryType);
					await SerializeAsync();
					return true;
				}
			}

			return false;
		}
		/// <summary>
		/// Helper method to process an iTunes PLIST into a List of Album objects.
		/// </summary>
		/// <returns>Whether the process was successful or not.</returns>
		private static bool ParseXml(string actualLibaryPath, PList libraryIndex, out IEnumerable<Album> outAlbums, out IEnumerable<Playlist> outPlaylists)
		{
			Contract.Requires(!string.IsNullOrEmpty(actualLibaryPath));
			Contract.Requires(libraryIndex != null);

			// file://localhost/C:/Users/Steve/Music/iTunes/iTunes%20Media/
			// file://localhost/Users/Steve/Music/iTunes/iTunes%20Media/

			try
			{
				string musicFolderString = libraryIndex.GetStringOrDefault("Music Folder");
				string declaredMediaPath = new Uri(musicFolderString).LocalPath;
				var absoluteMediaPath = GetAbsoluteMediaPathUri(actualLibaryPath, declaredMediaPath);

				IEnumerable<PList> trackListRaw = libraryIndex.GetOrDefault<PList>("Tracks").Values.Cast<PList>();
				IEnumerable<PList> playlistListRaw = libraryIndex.GetOrDefault<List<object>>("Playlists").Cast<PList>();

				if (string.IsNullOrEmpty(declaredMediaPath) || trackListRaw == null || playlistListRaw == null)
				{
					throw new InvalidOperationException();
				}

				Dictionary<UniqueAlbum, AlbumBuilder> albumBuilders = new Dictionary<UniqueAlbum, AlbumBuilder>();
				foreach (var trackInfoRaw in trackListRaw)
				{
					string trackLocationString = trackInfoRaw.GetStringOrDefault("Location");
					long trackBitRateLong = trackInfoRaw.GetLongOrDefault("Bit Rate");

					if (IsValidMediaKind(trackLocationString, trackBitRateLong))
					{
						string titleString = trackInfoRaw.GetStringOrDefault("Name");
						string albumString = trackInfoRaw.GetStringOrDefault("Album");
						string artistString = trackInfoRaw.GetStringOrDefault("Artist");
						string albumArtistString = trackInfoRaw.GetStringOrDefault("Album Artist") ?? artistString;
						int trackIDInt = trackInfoRaw.GetIntOrDefault("Track ID");
						int discNumberInt = trackInfoRaw.GetIntOrDefault("Disc Number");
						int trackNumberInt = trackInfoRaw.GetIntOrDefault("Track Number");
						int yearInt = trackInfoRaw.GetIntOrDefault("Year");
						int totalTimeInt = trackInfoRaw.GetIntOrDefault("Total Time");

						var relativeTrackLocation = new Uri(trackLocationString).LocalPath.Replace(declaredMediaPath, string.Empty);
						string formattedLocationString = Path.Combine(absoluteMediaPath, relativeTrackLocation);

						AlbumBuilder albumBuilder;
						var albumBuilderID = new UniqueAlbum(albumString, albumArtistString);
						if (!albumBuilders.TryGetValue(albumBuilderID, out albumBuilder))
						{
							albumBuilder = new AlbumBuilder(albumString, albumArtistString, yearInt);
							albumBuilders.Add(albumBuilderID, albumBuilder);
						}

						Contract.Assume(albumBuilder != null); // New AlbumBuilder should have been created if it doesn't yet exist.

						albumBuilder.AddTrack(
							trackID: trackIDInt,
							title: titleString,
							artist: artistString,
							discNumber: discNumberInt,
							trackNumber: trackNumberInt,
							location: formattedLocationString,
							totalTime: new TimeSpan(0, 0, totalTimeInt / 1000));
					}
				}

				IEnumerable<Album> libraryAlbumsUnsorted = albumBuilders.Values.AsParallel().Select(thatBuilder => thatBuilder.GetAlbum()).ToList();
				IEnumerable<Track> libraryTracksUnsorted = libraryAlbumsUnsorted.AsParallel().SelectMany(thatAlbum => thatAlbum.TrackList);

				List<Playlist> libraryPlaylists = new List<Playlist>();
				foreach (var playlistInfoRaw in playlistListRaw)
				{
					List<object> playlistItemsRaw = playlistInfoRaw.GetOrDefault<List<object>>("Playlist Items");
					if (playlistItemsRaw != null)
					{
						List<Track> playlistItems = playlistItemsRaw
							.Cast<PList>() // This is the only thing I can think of that would cause an exception in this method.
							.Select(thatPlist => thatPlist.GetIntOrDefault("Track ID"))
							.Join(libraryTracksUnsorted,
								itemIndex => itemIndex,
								track => track.TrackID,
								(index, track) => track).ToList();

						if (playlistItems.Count > 0)
						{
							libraryPlaylists.Add(new Playlist(playlistInfoRaw.GetStringOrDefault("Name"), playlistItems));
						}
					}
				}

				outAlbums = libraryAlbumsUnsorted.OrderBy(thatAlbum => thatAlbum.AlbumArtist).ThenBy(thatAlbum => thatAlbum.Title);
				outPlaylists = libraryPlaylists.OrderBy(playlist => playlist.Title);

				return true;
			}
			catch (Exception)
			{
				outAlbums = null;
				outPlaylists = null;

				return false;
			}
		}
		private static async Task SerializeAsync()
		{
			if (TunesDataSource.IsLoaded) // Make sure there is something to save
			{
				StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(ITUNES_CACHE_XML, CreationCollisionOption.ReplaceExisting);
				DataContractSerializer dcs = new DataContractSerializer(typeof(TunesDataSource));

				using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
				{
					var outputStream = stream.GetOutputStreamAt(0);
					dcs.WriteObject(outputStream.AsStreamForWrite(), TunesDataSource.Default);
				}

				Settings.LastLibraryUpdate = DateTimeOffset.UtcNow;
			}
		}

		#endregion Static members

		[DataMember(Name = "AlbumsFlat")]
		private readonly IEnumerable<Album> _albums;

		[DataMember(Name = "LibraryOS")]
		private readonly OperatingSystem _libraryType;

		[DataMember(Name = "PlaylistsFlat")]
		private readonly IEnumerable<Playlist> _playlists;

		/// <summary>
		/// Creates a new TunesDataSource.
		/// </summary>
		private TunesDataSource(IEnumerable<Album> albums, IEnumerable<Playlist> playlists, OperatingSystem libraryType)
		{
			_albums = albums;
			_playlists = playlists;
			_libraryType = libraryType;
		}

		/// <summary>
		/// Gets a flat collection of all albums, sorted by album artist.
		/// For most applications, this works well. Because LINQ is involved, the elements are only queried when needed.
		/// </summary>
		public IEnumerable<Album> AlbumsFlat
		{
			get
			{
				return _albums;
			}
		}

		/// <summary>
		/// Gets a playlist containing all tracks.
		/// </summary>
		public Playlist AllSongsPlaylist
		{
			get
			{
				return new Playlist(LocalizationManager.GetString("Items/Playlist/DefaultAllSongsTitle"), SongsFlat.ToList());
			}
		}

		/// <summary>
		/// Gets a flat collection of all playlists.
		/// </summary>
		public IEnumerable<Playlist> PlaylistsFlat
		{
			get
			{
				return _playlists;
			}
		}

		/// <summary>
		/// Gets a flat collection of all tracks.
		/// </summary>
		public IEnumerable<Track> SongsFlat
		{
			get
			{
				return AlbumsFlat.AsParallel().SelectMany(album => album.TrackList);
			}
		}

		/// <summary>
		/// Gets the OS on which the iTunes Library is maintained.
		/// </summary>
		internal OperatingSystem LibraryOS
		{
			get
			{
				return _libraryType;
			}
		}

		/// <summary>
		/// Searches through the iTunes Library for items.
		/// </summary>
		/// <typeparam name="T">The type of object wanted.</typeparam>
		/// <param name="query">The query to search for.</param>
		/// <returns>A collection of matching library items.</returns>
		public IEnumerable<ILibraryItem> GetSearchResults<T>(string query)
		{
			string capQuery = (query ?? string.Empty).ToUpperInvariant();

			IEnumerable<ILibraryItem> unfiltered;
			if (typeof(T) == typeof(Album))
			{
				unfiltered = AlbumsFlat.Cast<ILibraryItem>();
			}
			else if (typeof(T) == typeof(Playlist))
			{
				unfiltered = PlaylistsFlat.Cast<ILibraryItem>();
			}
			else if (typeof(T) == typeof(Track))
			{
				unfiltered = SongsFlat.Cast<ILibraryItem>();
			}
			else
			{
				unfiltered = Enumerable.Empty<ILibraryItem>();
			}

			return unfiltered.Where(item => item.Title.ToUpperInvariant().Contains(capQuery) || item.Subtitle.ToUpperInvariant().Contains(capQuery));
		}
	}
}