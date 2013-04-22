TuneOut
=======

A media player for Windows 8 that read iTunes libraries, including libraries on a Boot Camp partition.

License
=======
In general, most of the code in this repository is licensed under the Microsoft Public License (Ms-PL), a copy of which is available at http://opensource.org/licenses/MS-PL.
However, certain files are available only under separate licenses; these files will have separate credits and copyright information listed in their headers.

Getting Started with the Code
=============================
TuneOut is composed of two main projects: `TuneOut` and `TuneOut.Model`.
`TuneOut.Model` contains (barely) and UI components, and can be taken and used in pretty much any other Windows 8 Store application without any major problems.
It contains all of the code necessary to load iTunes libraries, play media, and create a "now playing" queue. All publically-accessible members in TuneOut.Model should have XML code documentation.

The solution will not compile right off-the-bat because it is missing `LastFmApiSecrets.cs`, which contains the info required to connect to Last.fm.
There is a sanitized file called `LastFmApiSecrets_Public.cs` which is available in the `TuneOut.Model` project, under the `TuneOut.AppData` namespace.
This file is set to not compile in Visual Studio. You can either set it to compile, or make a copy in Visual Studio. This will enable to solution to compile.
If you do not replace the placeholders with a [Last.fm API](http://www.last.fm/api/) key and shared secret, then album art and scrobbling will be unavailable (but everything else will work).

Basics
------
1. Set a library location using `TuneOut.Settings.SetLibraryLocation(Windows.Storage.StorageFolder)`. The location is the outer-most iTunes folder, the one that contains `iTunes Music Library.xml`.
2. Create a XAML `MediaElement` somewhere **IN THE XAML VISUAL TREE**, i.e. in the XAML code itself. This will cause the MediaElement to be created on the UI thread, avoiding many nasty problems.
3. Use `TuneOut.Audio.TunesDataSource.Load()` to load the iTunes Library. If you have already set the library location, then things should go pretty smoothly.
4. Initialize the default `TuneOut.AudioController` object by using `Audio.AudioController.Default.Ready(MediaElement)`. You must pass in the MediaElement that you created previously as a parameter.
5. Use `TunesDataSource.Default.SongsFlat`, `TunesDataSource.AlbumsFlat`, and `TunesDataSource.PlaylistsFlat` to get library items. Use the function in `AudioController.Default` to load items into the queue and start playback!
