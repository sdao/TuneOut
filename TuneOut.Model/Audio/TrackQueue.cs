using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;

namespace TuneOut.Audio
{
	/// <summary>
	/// Represents a queue of tracks that will, or have been, played.
	/// </summary>
	[DataContract]
	internal class TrackQueue
	{
		private ReversibleQueue<UniqueTrack> _trackList = new ReversibleQueue<UniqueTrack>();

		/// <summary>
		/// Returns the current track if available, or null if there is no current track.
		/// </summary>
		public Track Current
		{
			get
			{
				return _trackList == null ? null : _trackList.Current.Track;
			}
		}

		/// <summary>
		/// Returns the current unique track if available, or UniqueTrack.Empty if there is no current track.
		/// </summary>
		public UniqueTrack CurrentUnique
		{
			get
			{
				return _trackList == null ? UniqueTrack.Empty : _trackList.Current;
			}
		}

		/// <summary>
		/// Gets an observable collection with all upcoming, not-already-played tracks.
		/// </summary>
		public IReadOnlyObservableList<UniqueTrack> UpcomingTracks
		{
			get
			{
				return _trackList;
			}
		}

		/// <summary>
		/// Gets or sets whether the queue has been manually altered by the user.
		/// </summary>
		[DataMember]
		public bool UserMutatedQueue { get; private set; }
		/// <summary>
		/// Gets or sets a collection of the track IDs of the tracks in the backing list.
		/// </summary>
		[DataMember]
		private IEnumerable<int> SerializedTrackQueue
		{
			get
			{
				return _trackList.Current.Yield().Concat(_trackList).Select(track => track.Track.TrackID);
			}
			set
			{
				Contract.Requires(TunesDataSource.Default != null);

				var allTracks = TunesDataSource.Default.SongsFlat;
				var matchedTracks = from i in value
									join t in allTracks on i equals t.TrackID
									select t;

				_trackList = new ReversibleQueue<UniqueTrack>(matchedTracks.Select(x => x.UniqueTrack()));
				_trackList.Dequeue();
			}
		}

		/// <summary>
		/// Adds to the contents of the queue with a collection of tracks.
		/// Sets UserMutatedQueue.
		/// </summary>
		/// <param name="t">The songs.</param>
		public void Add(IEnumerable<Track> t)
		{
			Contract.Requires(t != null);

			UserMutatedQueue = true;

			var startIndex = _trackList.Count;
			var items = t.Select(x => x.UniqueTrack());
			_trackList.Enqueue(items);
			if (!_trackList.HasCurrent) _trackList.Dequeue();
		}

		/// <summary>
		/// Empty the contents of the track queue.
		/// </summary>
		public void Clear()
		{
			UserMutatedQueue = false;
			_trackList.Clear();
		}

		/// <summary>
		/// Advances the track queue backward once if possible.
		/// If there were no tracks to begin with, does nothing.
		/// If the first track is hit, does not clear the queue.
		/// </summary>
		/// <returns>If there are no tracks left after advancing, returns false. Otherwise, returns true.</returns>
		public bool MoveBack()
		{
			if (_trackList.DequeuedCount > 1)
			{
				_trackList.EnqueueBack();
			}

			return (_trackList.HasCurrent);
		}

		/// <summary>
		/// Advances the track queue forward once if possible.
		/// If there were no tracks to begin with, does nothing.
		/// If the last track is hit, clears the queue.
		/// </summary>
		/// <returns>If there are no tracks left after advancing, returns false. Otherwise, returns true.</returns>
		public bool MoveNext()
		{
			if (_trackList.Count == 0)
			{
				Clear();
			}
			else
			{
				_trackList.Dequeue();
			}

			return (_trackList.HasCurrent);
		}

		/// <summary>
		/// Advances the track queue forward to a specified element is possible.
		/// If there were no tracks to begin with, does nothing.
		/// If the last track is hit, clears the queue.
		/// </summary>
		/// <param name="t">The track to advance to.</param>
		/// <returns>If there are no tracks left after advancing, returns false. Otherwise, returns true.</returns>
		public bool MoveTo(UniqueTrack t)
		{
			if (_trackList.Count == 0)
			{
				Clear();
			}
			else
			{
				int index = _trackList.IndexOf(t);
				if (index != -1)
				{
					_trackList.Dequeue(index);
				}
			}

			return (_trackList.HasCurrent);
		}
		/// <summary>
		/// Removes an upcoming track in the queue.
		/// Sets UserMutatedQueue to true.
		/// </summary>
		/// <param name="t">The track to remove.</param>
		public bool Remove(UniqueTrack t)
		{
			bool removed = _trackList.Remove(t);
			if (removed)
			{
				UserMutatedQueue = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Removes the current track in the queue. Generally used in the case where the track is unplayable.
		/// Does not set UserMutatedQueue.
		/// </summary>
		/// <returns>The removed track, or null if no track was removed.</returns>
		public Track RemoveCurrent()
		{
			if (!_trackList.HasCurrent)
			{
				return null;
			}
			else if (_trackList.Count == 0)
			{
				var track = _trackList.Current;
				Clear();

				return track.Track;
			}
			else
			{
				return _trackList.Kill().Track;
			}
		}

		/// <summary>
		/// Replaces the entire track queue with a single song, queuing up the other items in the same track container.
		/// You should check the UserMutatedQueue property before performing a destructive replacement.
		/// </summary>
		/// <param name="t">The song.</param>
		/// <param name="a">The track container that holds the song.</param>
		public void ReplaceAllWithSong(Track t, ITrackContainer a)
		{
			Contract.Requires(t != null);
			Contract.Requires(a != null);
			Contract.Requires(a.TrackList != null);
			Contract.Requires(a.TrackList.Count != 0);

			_trackList.BatchChanges();

			Clear();

			var items = a.TrackList.Select(x => x.UniqueTrack());
			_trackList.Enqueue(items);

			int trackOffset = Math.Max(0, a.IndexOf(t));
			_trackList.Dequeue(trackOffset);

			_trackList.FlushChanges();
		}

		/// <summary>
		/// Replaces the entire track queue with the contents of a track container. You should check the UserMutatedQueue property before performing a destructive replacement.
		/// </summary>
		/// <param name="a">The track container.</param>
		/// <param name="shuffle">Whether to shuffle the order in which the track container is added.</param>
		public void ReplaceAllWithTrackContainer(ITrackContainer a, bool shuffle)
		{
			Contract.Requires(a != null);
			Contract.Requires(a.TrackList != null);
			Contract.Requires(a.TrackList.Count != 0);

			_trackList.BatchChanges();

			Clear();

			if (shuffle)
			{
				var items = a.TrackList.Select(x => x.UniqueTrack()).OrderBy(x => x.UniqueId);
				_trackList.Enqueue(items);
				_trackList.Dequeue();
			}
			else
			{
				var items = a.TrackList.Select(x => x.UniqueTrack());
				_trackList.Enqueue(items);
				_trackList.Dequeue();
			}

			_trackList.FlushChanges();
		}
		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(_trackList != null);
		}
	}
}