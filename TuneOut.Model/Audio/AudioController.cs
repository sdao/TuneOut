using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using TuneOut.AppData;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TuneOut.Audio
{
	/// <summary>
	/// An audio controller that manages audio transport and track queues
	/// when connected to a <seealso cref="MediaElement"/> control.
	/// </summary>
	public class AudioController : INotifyPropertyChanged
	{
		private const string QUEUE_SUSPENSION_XML = "_queue.xml";

		private static readonly AudioController _Default = new AudioController();

		private readonly Object _syncRoot = new Object();
		private readonly DispatcherTimer _timer = new DispatcherTimer();

		private TimeSpan _InternalPosition = TimeSpan.Zero;
		private MediaElement _media = new MediaElement();

		/// <summary>
		/// Do not perform mutations on this object without routing through the <seealso cref="MutateQueue(System.Func&lt;bool&gt;)"/> or <seealso cref="MutateQueue(System.Action)"/> methods.
		/// </summary>
		private TrackQueue _queue = new TrackQueue();

		private AudioControllerStatus _Status = AudioControllerStatus.NotReady;

		/// <summary>
		/// Creates an empty audio controller.
		/// </summary>
		private AudioController()
		{
			_timer = new DispatcherTimer();
			_timer.Interval = new TimeSpan(0, 0, 0, 0, 250);
			_timer.Tick += (sender, eventArgs) =>
			{
				InternalPosition = _media.Position;
			};

			// Configure media controls
			Windows.Media.MediaControl.PlayPressed += MediaControl_PlayPressed;
			Windows.Media.MediaControl.StopPressed += MediaControl_StopPressed;
			Windows.Media.MediaControl.PlayPauseTogglePressed += MediaControl_PlayPauseTogglePressed;
			Windows.Media.MediaControl.PausePressed += MediaControl_PausePressed;
			Windows.Media.MediaControl.NextTrackPressed += MediaControl_NextTrackPressed;
			Windows.Media.MediaControl.PreviousTrackPressed += MediaControl_PreviousTrackPressed;

			// Scrobbling
			LastFmScrobbler.Default.ConnectAudioController(this);
		}

		/// <summary>
		/// Gets the default singleton instance.
		/// </summary>
		public static AudioController Default
		{
			get
			{
				Contract.Ensures(Contract.Result<AudioController>() != null);
				return _Default;
			}
		}
		/// <summary>
		/// Gets the current track in the queue.
		/// </summary>
		public Track Current
		{
			get
			{
				return _queue.Current;
			}
		}

		/// <summary>
		/// Gets or sets whether the volume is muted.
		/// Does nothing if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.
		/// </summary>
		public bool IsMuted
		{
			get
			{
				return _media == null ? false : _media.IsMuted;
			}
			set
			{
				if (_media != null)
				{
					_media.IsMuted = value;
					Settings.IsMuted = value;
					OnPropertyChanged("IsMuted");
				}
			}
		}

		/// <summary>
		/// Gets the Play To source for the current media element.
		/// </summary>
		public Windows.Media.PlayTo.PlayToSource PlayToSource
		{
			get
			{
				return _media == null ? null : _media.PlayToSource;
			}
		}

		/// <summary>
		/// Gets or sets the current playing position.
		/// If a track is currently playing, setting this property
		/// will seek within the track. Otherwise, the next track playing
		/// will begin at this position.
		/// </summary>
		public TimeSpan Position
		{
			get
			{
				return InternalPosition;
			}

			set
			{
				InternalPosition = value;

				if (_media != null)
				{
					_media.Position = _InternalPosition;
				}
			}
		}

		/// <summary>
		/// Gets the current status of the audio transport mechanism.
		/// </summary>
		public AudioControllerStatus Status
		{
			get
			{
				return _Status;
			}

			private set
			{
				if (value != _Status)
				{
					switch (value)
					{
						case AudioControllerStatus.Inactive:
							_timer.Stop();
							_Status = AudioControllerStatus.Inactive;
							break;

						case AudioControllerStatus.Paused:
							_timer.Stop();
							_Status = AudioControllerStatus.Paused;
							break;

						case AudioControllerStatus.Playing:
							_timer.Start();
							_Status = AudioControllerStatus.Playing;
							break;
					}

					UserMutationCount++;
					OnStatusChanged();
				}
			}
		}

		/// <summary>
		/// Gets a read-only observable collection of all tracks that will be played, but have not been played yet.
		/// </summary>
		public IReadOnlyObservableList<UniqueTrack> UpcomingTracks
		{
			get
			{
				return _queue.UpcomingTracks;
			}
		}

		/// <summary>
		/// Gets whether the user has performed single-item queue mutations.
		/// If true, all actions that result in replacing the queue should prompt the user.
		/// </summary>
		public bool UserMutatedQueue
		{
			get
			{
				return _queue.UserMutatedQueue;
			}
		}

		/// <summary>
		/// Gets or sets the volume on a [0.0, 100.0] scale.
		/// Does nothing if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.
		/// </summary>
		public double Volume
		{
			get
			{
				return _media == null ? 1d : _media.Volume * 100d;
			}

			set
			{
				if (_media != null)
				{
					_media.Volume = value / 100d;
					Settings.Volume = value;
					OnPropertyChanged("Volume");
				}
			}
		}

		/// <summary>
		/// Gets the current unique track in the queue.
		/// </summary>
		private UniqueTrack CurrentUnique
		{
			get
			{
				return _queue.CurrentUnique;
			}
		}

		/// <summary>
		/// Gets or sets the current playing position without re-updating the media element.
		/// Use this property to perform internal updates coming from the media element itself.
		/// </summary>
		private TimeSpan InternalPosition
		{
			get
			{
				return _InternalPosition;
			}
			set
			{
				_InternalPosition = value;
				OnPropertyChanged("Position");
			}
		}

		/// <summary>
		/// Gets whether an audio file is currently open.
		/// </summary>
		private bool TrackStreamOpened
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the time at which the current track was opened for playback.
		/// </summary>
		private DateTime TrackStreamOpenTimeUtc
		{
			get;
			set;
		}

		/// <summary>
		/// Counts the number of times the <seealso cref="Status"/> or <seealso cref="CurrentUnique"/>
		/// properties have changed.
		/// </summary>
		private int UserMutationCount
		{
			get;
			set;
		}

		/// <summary>
		/// Adds a collection of items to the queue.
		/// </summary>
		/// <param name="items">The collection to add.</param>
		/// <param name="play">Whether to immediately start playing.</param>
		public void Add(IEnumerable<Track> items, bool play)
		{
			MutateQueue(() => _queue.Add(items));

			if (Status == AudioControllerStatus.Inactive && play) Play();
		}

		/// <summary>
		/// Adds a track container to the queue.
		/// </summary>
		/// <param name="trackContainer">The track container to add.</param>
		/// <param name="play">Whether to immediately start playing.</param>
		public void Add(ITrackContainer trackContainer, bool play)
		{
			MutateQueue(() => _queue.Add(trackContainer.TrackList));

			if (Status == AudioControllerStatus.Inactive && play) Play();
		}

		/// <summary>
		/// Leaves the current track loaded and temporarily pauses playback.
		/// Does nothing if the <seealso cref="Status"/> is not <seealso cref="AudioControllerStatus.Playing"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		public void Pause()
		{
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			if (Status == AudioControllerStatus.Playing && TrackStreamOpened)
			{
				Status = AudioControllerStatus.Paused;
				_media.Pause();
			}
		}

		/// <summary>
		/// Attempts to start or continue playback of the current track.
		/// If the current track is not loaded into memory, its file will  be opened.
		/// Does nothing if the <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.Playing"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		public void Play()
		{
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			if (Status != AudioControllerStatus.Playing)
			{
				if (Status == AudioControllerStatus.Paused && TrackStreamOpened)
				{
					// Attempt to continue playback at current position.
					Status = AudioControllerStatus.Playing;
					_media.Play();
				}
				else if (Current != null)
				{
					// Open the track stream if necessary.
					Status = AudioControllerStatus.Playing;
					OpenTrackStream(resetPosition: false);
				}
				else
				{
					Stop();
				}
			}
		}

		/// <summary>
		/// Attemps to start playback of a particular track.
		/// Its file wille be opened if, and only if, the track can be found.
		/// Otherwise, nothing will happen.
		/// </summary>
		/// <param name="t">The track to play.</param>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		public void Play(UniqueTrack t)
		{
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			if (MoveTo(t))
			{
				Status = AudioControllerStatus.Playing;
				OpenTrackStream(resetPosition: true);
			}
		}

		/// <summary>
		/// If <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>,
		/// readies the audio controller with a media element.
		/// </summary>
		/// <param name="me">The media element to use for playback. It should be attached to the visual tree, on the UI thread.</param>
		public async void Ready(MediaElement me)
		{
			if (Status == AudioControllerStatus.NotReady)
			{
				_media = me;
				_media.AudioCategory = Windows.UI.Xaml.Media.AudioCategory.BackgroundCapableMedia;
				_media.AutoPlay = true;
				_media.MediaEnded += _media_MediaEnded;
				_media.MediaFailed += _media_MediaFailed;

				Volume = Settings.Volume;
				IsMuted = Settings.IsMuted;
				Status = AudioControllerStatus.Inactive;

				await DeserializeAsync();
			}
		}

		/// <summary>
		/// Removes a single track from the queue.
		/// </summary>
		/// <param name="t">The track to remove.</param>
		public void Remove(UniqueTrack t)
		{
			MutateQueue(() => _queue.Remove(t));
		}

		/// <summary>
		/// Replaces the queue by a track container.
		/// </summary>
		/// <param name="trackContainer">The track container to add.</param>
		/// <param name="shuffle">Whether to shuffle the tracks when adding.</param>
		/// <param name="play">Whether to immediately start playing.</param>
		public void ReplaceAll(ITrackContainer trackContainer, bool shuffle, bool play)
		{
			Contract.Requires<ArgumentNullException>(trackContainer != null);
			Contract.Requires<ArgumentException>(trackContainer.TrackList.Count > 0);

			Stop();

			MutateQueue(() => _queue.ReplaceAllWithTrackContainer(trackContainer, shuffle));

			if (play) Play();
		}

		/// <summary>
		/// Replaces the queue by a single track.
		/// </summary>
		/// <param name="t">The track to add.</param>
		/// <param name="trackContainer">A track container that contains the track <paramref name="t"/>.</param>
		/// <param name="play">Whether to immediately start playing.</param>
		public void ReplaceAll(Track t, ITrackContainer trackContainer, bool play)
		{
			Contract.Requires<ArgumentException>(t != null);
			Contract.Requires<ArgumentNullException>(trackContainer != null);
			Contract.Requires<ArgumentException>(trackContainer.TrackList.Count > 0);

			Stop();

			MutateQueue(() => _queue.ReplaceAllWithSong(t, trackContainer));

			if (play) Play();
		}

		/// <summary>
		/// Attempts to play the current track, or re-play it from the beginning
		/// if it is currently open. Will force a load or a re-load from memory.
		/// </summary>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		public void Replay()
		{
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			if (Current != null)
			{
				// Open the track stream if necessary.
				Status = AudioControllerStatus.Playing;
				OpenTrackStream(resetPosition: true);
			}
			else
			{
				Stop();
			}
		}

		/// <summary>
		/// Saves the AudioController on suspension.
		/// </summary>
		/// <returns>An async Task.</returns>
		public async Task SerializeAsync()
		{
			if (_queue.Current != null) // Make sure there is something to save
			{
				StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(QUEUE_SUSPENSION_XML, CreationCollisionOption.ReplaceExisting);
				DataContractSerializer dcs = new DataContractSerializer(typeof(TrackQueue));

				using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
				{
					var outputStream = stream.GetOutputStreamAt(0);
					dcs.WriteObject(outputStream.AsStreamForWrite(), _queue);
				}
			}
		}

		/// <summary>
		/// Plays the next track if one exists.
		/// Otherwise, stops playback.
		/// </summary>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		public void SkipAhead()
		{
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			if (Status != AudioControllerStatus.Inactive)
			{
				if (MoveNext())
				{
					// Play the new current track
					Status = AudioControllerStatus.Playing;
					OpenTrackStream(resetPosition: true);
				}
				else
				{
					Stop();
				}
			}
		}

		/// <summary>
		/// Seeks the current track to <seealso cref="TimeSpan.Zero"/> if there is no previous track,
		/// or the current song has been playing for more than two seconds.
		/// Otherwise, plays the previous track.
		/// </summary>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		public void SkipBack()
		{
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			if (Status != AudioControllerStatus.Inactive)
			{
				if (Position < new TimeSpan(0, 0, 2) && MoveBack())
				{
					// Play the new current track
					Status = AudioControllerStatus.Playing;
					OpenTrackStream(resetPosition: true);
				}
				else if (TrackStreamOpened)
				{
					// Rewind track
					Status = AudioControllerStatus.Playing;
					Position = TimeSpan.Zero;
					_media.Play();
				}
			}
		}

		/// <summary>
		/// Stops playback and clears the queue.
		/// </summary>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		public void Stop()
		{
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			ClearQueue();
			_media.Stop();
			_media.Source = null;

			Position = TimeSpan.Zero;
			TrackStreamOpened = false;
			TrackStreamOpenTimeUtc = DateTime.MinValue;
			Status = AudioControllerStatus.Inactive;
		}

		private void _media_MediaEnded(object sender, RoutedEventArgs e)
		{
			SkipAhead();
		}

		private void _media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
		{
			HandleStreamError();
		}

		/// <summary>
		/// Clears the contents of the queue.
		/// </summary>
		private void ClearQueue()
		{
			MutateQueue(() => _queue.Clear());
		}

		/// <summary>
		/// Restores the AudioController from suspension.
		/// </summary>
		/// <remarks>This method should only be called from the constructor.</remarks>
		private async Task DeserializeAsync()
		{
			StorageFile file = null;
			TrackQueue deserializedQueue = null;

			try
			{
				file = await ApplicationData.Current.LocalFolder.GetFileAsync(QUEUE_SUSPENSION_XML);

				using (IInputStream stream = await file.OpenReadAsync())
				{
					DataContractSerializer dcs = new DataContractSerializer(typeof(TrackQueue));
					deserializedQueue = (TrackQueue)dcs.ReadObject(stream.AsStreamForRead());
				}
			}
			catch (Exception)
			{
				// Justification: deserialization failure should assume corrupt file and fail gracefully
			}

			if (deserializedQueue != null)
			{
				MutateQueue(() => _queue = deserializedQueue);
				OnPropertyChanged("UpcomingTracks"); // In this case, a new object was deserialized.
				if (_queue.Current != null) Status = AudioControllerStatus.Paused;
			}

			if (file != null)
			{
				await file.DeleteAsync();
			}
		}

		/// <summary>
		/// Handles a track streaming error. Will simulate two seconds of inactivity to appear
		/// as though the system is attempting to open the file, and,
		/// if the user has not taken action after the two seconds, finally
		/// reports the error. In the event that the user changes the track within
		/// the two seconds or pauses the audio transport, does nothing.
		/// </summary>
		/// <exception cref="InvalidOperationException">if <seealso cref="Current"/> is null.</exception>
		private async void HandleStreamError()
		{
			Contract.Requires<InvalidOperationException>(Current != null);

			_media.Stop();
			_media.Source = null;
			TrackStreamOpened = false;
			TrackStreamOpenTimeUtc = DateTime.MinValue;
			Position = TimeSpan.Zero;

			UniqueTrack errorTrack = CurrentUnique;
			var currentMutationCount = UserMutationCount;

			await Task.Delay(2000);

			// Only advance the track if the user has not taken any intervening action in the mean-time.
			if (currentMutationCount == UserMutationCount)
			{
				// Report the error.
				OnCurrentTrackFailed(errorTrack);

				// Remove the track from the queue.
				RemoveCurrent();

				// Play the current track.
				Replay();
			}
		}

		private async void MediaControl_NextTrackPressed(object sender, object e)
		{
			if (Status == AudioControllerStatus.Paused || Status == AudioControllerStatus.Playing)
			{
				await _media.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					SkipAhead();
				});
			}
		}

		private async void MediaControl_PausePressed(object sender, object e)
		{
			if (Status == AudioControllerStatus.Playing)
			{
				await _media.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					Pause();
				});
			}
		}

		private async void MediaControl_PlayPauseTogglePressed(object sender, object e)
		{
			if (Status == AudioControllerStatus.Paused)
			{
				await _media.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					Play();
				});
			}
			else if (Status == AudioControllerStatus.Playing)
			{
				await _media.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					Pause();
				});
			}
		}

		private async void MediaControl_PlayPressed(object sender, object e)
		{
			if (Status == AudioControllerStatus.Paused)
			{
				await _media.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					Play();
				});
			}
		}

		private async void MediaControl_PreviousTrackPressed(object sender, object e)
		{
			if (Status == AudioControllerStatus.Paused || Status == AudioControllerStatus.Playing)
			{
				await _media.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					SkipBack();
				});
			}
		}
		private async void MediaControl_StopPressed(object sender, object e)
		{
			if (Status == AudioControllerStatus.Playing)
			{
				await _media.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					Pause();
				});
			}
		}
		#region Event handling

		/// <summary>
		/// Occurs when the current track has changed.
		/// The event arguments sent will contain the track after the change, which may be null.
		/// </summary>
		public event EventHandler<TrackChangedEventArgs> CurrentTrackChanged;

		/// <summary>
		/// Occurs when a failure occurs in attempting to play the current track.
		/// </summary>
		/// <remarks>This event occurs approximately two seconds after the actual failure.
		/// This happens by design, so that successive failures cannot block the UI.
		/// In the event that a transport command is set before this two-second period,
		/// this event will not occur.</remarks>
		public event EventHandler<TrackEventArgs> CurrentTrackFailed;

		/// <summary>
		/// Occurs when a property value changes.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private event EventHandler<AudioControllerStatusEventArgs> _StatusChanged;

		/// <summary>
		/// Occurs when the status of the <seealso cref="AudioController"/> instance changes, for example,
		/// due to the the audio being paused or started.
		/// When a new handler is added to StatusChanged, it will be called immediately to report the status.
		/// </summary>
		public event EventHandler<AudioControllerStatusEventArgs> StatusChanged
		{
			add
			{
				if (value != null)
				{
					lock (_syncRoot)
					{
						_StatusChanged += value;
					}


					value(this, new AudioControllerStatusEventArgs(Status));
				}
			}

			remove
			{
				lock (_syncRoot)
				{
					_StatusChanged -= value;
				}
			}
		}

		/// <summary>
		/// Calls the <seealso cref="CurrentTrackChanged"/> event handler if it is non-null.
		/// </summary>
		/// <param name="oldTrackInfo">Information encapsulating the state of the previous track before the track changed.</param>
		protected void OnCurrentTrackChanged(TrackEventArgs oldTrackInfo)
		{
			Contract.Requires(oldTrackInfo != null);

			if (!oldTrackInfo.UniqueTrack.Equals(CurrentUnique))
			{
				if (oldTrackInfo.UniqueTrack.Track != null)
				{
					oldTrackInfo.UniqueTrack.Track.NotifyNowPlaying();
				}

				if (Current != null)
				{
					Current.NotifyNowPlaying();
					Windows.Media.MediaControl.AlbumArt = Current.Artwork.Image;
					Windows.Media.MediaControl.ArtistName = Current.Artist;
					Windows.Media.MediaControl.TrackName = Current.Title;
				}
				else
				{
					Windows.Media.MediaControl.AlbumArt = null;
					Windows.Media.MediaControl.ArtistName = string.Empty;
					Windows.Media.MediaControl.TrackName = string.Empty;
				}

				EventHandler<TrackChangedEventArgs> h = CurrentTrackChanged;

				if (h != null)
				{
					h(this, new TrackChangedEventArgs(oldTrackInfo, CurrentUnique));
				}

				UserMutationCount++;
				OnPropertyChanged("Current");
			}
		}

		/// <summary>
		/// Calls the <seealso cref="CurrentTrackFailed"/> event handler if it is non-null.
		/// </summary>
		/// <param name="t">The <seealso cref="UniqueTrack"/> encapsulating the <seealso cref="Track"/> whose playback has failed.</param>
		protected void OnCurrentTrackFailed(UniqueTrack t)
		{
			EventHandler<TrackEventArgs> h = CurrentTrackFailed;

			if (h != null)
			{
				h(this, new TrackEventArgs(t));
			}
		}

		/// <summary>
		/// Calls the <seealso cref="PropertyChanged"/> event handler if it is non-null.
		/// </summary>
		/// <param name="propertyName">The name of the property whose value has changed.</param>
		protected virtual void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler h = PropertyChanged;

			if (h != null)
			{
				h(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		/// <summary>
		/// Calls the <seealso cref="StatusChanged"/> event handler if it is non-null.
		/// </summary>
		protected void OnStatusChanged()
		{
			if (Status == AudioControllerStatus.Playing)
			{
				Windows.Media.MediaControl.IsPlaying = true;
			}
			else
			{
				Windows.Media.MediaControl.IsPlaying = false;
			}

			EventHandler<AudioControllerStatusEventArgs> h = _StatusChanged;

			if (h != null)
			{
				h(this, new AudioControllerStatusEventArgs(Status));
			}

			OnPropertyChanged("Status");
		}

		#endregion Event handling

		/// <summary>
		/// Moves the queue back one track.
		/// </summary>
		/// <returns>Whether the queue's <seealso cref="Current"/> property is non-null.</returns>
		private bool MoveBack()
		{
			return MutateQueue(() => _queue.MoveBack());
		}

		/// <summary>
		/// Moves the queue forward one track.
		/// </summary>
		/// <returns>Whether the queue's <seealso cref="Current"/> property is non-null.</returns>
		private bool MoveNext()
		{
			return MutateQueue(() => _queue.MoveNext());
		}

		/// <summary>
		/// Advances the queue to a specific track, if it can be found.
		/// </summary>
		/// <param name="t">The track to advance to.</param>
		/// <returns>Whether the queue's <seealso cref="Current"/> property is non-null.</returns>
		private bool MoveTo(UniqueTrack t)
		{
			return MutateQueue(() => _queue.MoveTo(t));
		}

		/// <summary>
		/// All queue mutation within the AudioController must go through this routing method.
		/// This method send the appropriate track-change messages if necessary.
		/// </summary>
		/// <param name="queueMutationFunction">An anonymous function that returns a bool.</param>
		/// <returns>The result of the anonymous function.</returns>
		/// <exception cref="ArgumentNullException">if <paramref name="queueMutationFunction"/> is null.</exception>
		private bool MutateQueue(Func<bool> queueMutationFunction)
		{
			Contract.Requires<ArgumentNullException>(queueMutationFunction != null);

			var storedTrack = new TrackEventArgs(CurrentUnique, TrackStreamOpenTimeUtc, Position);
			var result = queueMutationFunction();
			OnCurrentTrackChanged(storedTrack);

			return result;
		}

		/// <summary>
		/// All queue mutation within the AudioController must go through this routing method.
		/// This method send the appropriate track-change messages if necessary.
		/// </summary>
		/// <param name="queueMutationFunction">An anonymous function that returns void.</param>
		/// <exception cref="ArgumentNullException">if <paramref name="queueMutationFunction"/> is null.</exception>
		private void MutateQueue(Action queueMutationFunction)
		{
			Contract.Requires<ArgumentNullException>(queueMutationFunction != null);

			var storedTrack = new TrackEventArgs(CurrentUnique, TrackStreamOpenTimeUtc, Position);
			queueMutationFunction();
			OnCurrentTrackChanged(storedTrack);
		}

		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(_queue != null);
		}

		/// <summary>
		/// Attempts to open the current track file and begin playback.
		/// </summary>
		/// <param name="resetPosition">Whether to reset <seealso cref="Position"/> to <seealso cref="TimeSpan.Zero"/> before attempting to open the file.
		/// If false, the file will be loaded to the current <seealso cref="Position"/>.</param>
		/// <exception cref="InvalidOperationException">if <seealso cref="Current"/> is null.</exception>
		/// <exception cref="InvalidOperationException">if <seealso cref="Status"/> is <seealso cref="AudioControllerStatus.NotReady"/>.</exception>
		private async void OpenTrackStream(bool resetPosition = false)
		{
			Contract.Requires<InvalidOperationException>(Current != null);
			Contract.Requires<InvalidOperationException>(Status != AudioControllerStatus.NotReady);

			try
			{
				//Test error message bar
				//if (new Random().NextDouble() > 0.5)
				//{
				//	throw new Exception();
				//}

				var file = await StorageFile.GetFileFromPathAsync(Current.Location);
				var stream = await file.OpenReadAsync(TunesDataSource.Default.LibraryOS);
				_media.SetSource(stream, file.ContentType);

				if (resetPosition)
				{
					Position = TimeSpan.Zero;
				}
				else if (Position != TimeSpan.Zero)
				{
					_media.Position = Position;
				}

				TrackStreamOpened = true;
				TrackStreamOpenTimeUtc = DateTime.UtcNow;
			}
			catch (Exception)
			{
				HandleStreamError();
			}
		}

		/// <summary>
		/// Removes the track in the property <seealso cref="Current"/> from the queue.
		/// </summary>
		private void RemoveCurrent()
		{
			MutateQueue(() => _queue.RemoveCurrent());
		}
	}

	/// <summary>
	/// Represents the possible states of an <seealso cref="AudioController"/>.
	/// </summary>
	public enum AudioControllerStatus
	{
		/// <summary>
		/// Indicates that the audio controller has not been initialized, and any attempt to use it may result in failure.
		/// </summary>
		NotReady,

		/// <summary>
		/// Indicates that the audio controller has nothing queued up.
		/// </summary>
		Inactive,

		/// <summary>
		/// Indicates that the audio controller is currently playing.
		/// </summary>
		Playing,

		/// <summary>
		/// Indicates that the audio controller was playing, but is now paused.
		/// </summary>
		Paused
	}

	/// <summary>
	/// Provides data for the <seealso cref="AudioController.StatusChanged"/> event.
	/// </summary>
	public class AudioControllerStatusEventArgs : EventArgs
	{
		/// <summary>
		/// Creates a new <seealso cref="AudioControllerStatusEventArgs"/>.
		/// </summary>
		/// <param name="status">The new status.</param>
		public AudioControllerStatusEventArgs(AudioControllerStatus status)
		{
			Status = status;
		}

		/// <summary>
		/// Gets the new status.
		/// </summary>
		public AudioControllerStatus Status { get; private set; }
	}

	/// <summary>
	/// Provides data for the <seealso cref="AudioController.CurrentTrackChanged"/> event.
	/// </summary>
	public class TrackChangedEventArgs : TrackEventArgs
	{
		private readonly UniqueTrack _NewUniqueTrack;

		/// <summary>
		/// Creates a new <seealso cref="TrackChangedEventArgs"/>.
		/// </summary>
		/// <param name="oldTrack">The track that was playing before the track change.</param>
		/// <param name="newTrack">The track that started playing after the track change.</param>
		public TrackChangedEventArgs(TrackEventArgs oldTrack, UniqueTrack newTrack)
			: base(oldTrack.UniqueTrack, oldTrack.StartTimeUtc, oldTrack.Position)
		{
			Contract.Requires(oldTrack != null);
			_NewUniqueTrack = newTrack;
		}

		/// <summary>
		/// Gets the <seealso cref="Track"/> encapsulated by the <seealso cref="TrackChangedEventArgs.NewUniqueTrack"/> property.
		/// </summary>
		public Track NewTrack { get { return NewUniqueTrack.Track; } }

		/// <summary>
		/// Gets the <seealso cref="UniqueTrack"/> associated with the new state.
		/// </summary>
		public UniqueTrack NewUniqueTrack { get { return _NewUniqueTrack; } }
	}

	/// <summary>
	/// Provides data for several events in <seealso cref="AudioController"/>.
	/// </summary>
	public class TrackEventArgs : EventArgs
	{
		private readonly TimeSpan _Position;

		private readonly DateTime _StartTimeUtc;

		private readonly UniqueTrack _UniqueTrack;

		/// <summary>
		/// Creates a new <seealso cref="TrackEventArgs"/>.
		/// </summary>
		/// <param name="t">The <seealso cref="UniqueTrack"/> associated with the previous state.</param>
		/// <param name="time">Optional. The time when the track started playing.</param>
		/// <param name="position">Optional. The position of track playback.</param>
		public TrackEventArgs(UniqueTrack t, DateTime time = new DateTime(), TimeSpan position = new TimeSpan())
		{
			_UniqueTrack = t;
			_StartTimeUtc = time;
			_Position = position;
		}

		/// <summary>
		/// Gets the position of track playback.
		/// </summary>
		public TimeSpan Position { get { return _Position; } }

		/// <summary>
		/// Gets the time when the track started playing.
		/// </summary>
		public DateTime StartTimeUtc { get { return _StartTimeUtc; } }

		/// <summary>
		/// Gets the <seealso cref="Track"/> encapsulated by the <seealso cref="TrackEventArgs.UniqueTrack"/> property.
		/// </summary>
		public Track Track { get { return UniqueTrack.Track; } }

		/// <summary>
		/// Gets the <seealso cref="UniqueTrack"/> associated with the previous state.
		/// </summary>
		public UniqueTrack UniqueTrack { get { return _UniqueTrack; } }
	}
}