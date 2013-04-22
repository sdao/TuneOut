using Callisto.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using TuneOut.AppData;
using TuneOut.Audio;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace TuneOut
{
	/// <summary>
	/// An wasEmpty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : TuneOut.Common.LayoutAwarePage, IDisposable
	{
		private const int QUEUE_DISPLAY_LIMIT = 8;

		Menu _playMenu;
		private Flyout _playFlyout;
		private Flyout _volumeFlyout;
		private Flyout _alertFlyout;
		RoutedEventHandler _alertHandler;

		private Dictionary<string, object> _pageState;
		private Guid? _navigationParameter;

		#region Init/deinit code

		public MainPage()
		{
			Contract.Requires(TunesDataSource.IsLoaded);

			this.InitializeComponent();

			// Initialize Play All _playMenu; this can only occur in code-behind for the moment
			_playMenu = new Menu();

			MenuItem playAllItem = new MenuItem();
			playAllItem.Text = LocalizationManager.GetString("TransportControls/PlayMenu/PlayAll");
			playAllItem.Tapped += (thatSender, thoseEventArgs) =>
			{
				AudioController.Default.ReplaceAll(TunesDataSource.Default.AllSongsPlaylist, false, true);
			};

			MenuItem shuffleItem = new MenuItem();
			shuffleItem.Text = LocalizationManager.GetString("TransportControls/PlayMenu/ShuffleAll");
			shuffleItem.Tapped += (thatSender, thosEventArgs) =>
			{
				AudioController.Default.ReplaceAll(TunesDataSource.Default.AllSongsPlaylist, true, true);
			};

			_playMenu.Items.Add(playAllItem);
			_playMenu.Items.Add(shuffleItem);
		}

		public void Dispose()
		{
			_volumeFlyout.DisposeIfNonNull();
			_playFlyout.DisposeIfNonNull();
			_alertFlyout.DisposeIfNonNull();
			ACProxy.DisposeIfNonNull();
		}

		/// <summary>
		/// Populates the page with content passed during navigation.  Any saved state is also
		/// provided when recreating a page from a prior session.
		/// </summary>
		/// <param name="navigationParameter">The parameter value passed to
		/// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
		/// </param>
		/// <param name="pageState">A dictionary of state preserved by this page during an earlier
		/// session.  This will be null the first time a page is visited.</param>
		protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
		{
			// Save navigationParameter and pageState for the Loaded() handler
			_pageState = pageState;
			_navigationParameter = navigationParameter as Guid?;
		}

		/// <summary>
		/// Preserves state associated with this page in case the application is suspended or the
		/// page is discarded from the navigation cache.  Values must conform to the serialization
		/// requirements of <see cref="SuspensionManager.SessionState"/>.
		/// </summary>
		/// <param name="pageState">An wasEmpty dictionary to be populated with serializable state.</param>
		protected override void SaveState(Dictionary<String, Object> pageState)
		{
			pageState["PageMode"] = this.PageMode.AssemblyQualifiedName;
			pageState["AlbumScroll"] = FindVisualChild<ScrollViewer>(albumGridView).HorizontalOffset;
		}

		/// <summary>
		/// Set up UI and handlers here.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			// Start receiving notifications for volume app bar button
			ACProxy.IsSynchronizingValues = true;

			// Load the audio controller
			AudioController.Default.CurrentTrackFailed += AudioController_CurrentTrackFailed;
			AudioController.Default.StatusChanged += AudioController_StatusChanged;

			// Enable type-to-search
			Windows.ApplicationModel.Search.SearchPane.GetForCurrentView().ShowOnKeyboardInput = true;

			// Enable PlayTo selection
			Windows.Media.PlayTo.PlayToManager.GetForCurrentView().SourceRequested += MainPage_SourceRequested;
		}

		/// <summary>
		/// Clean up UI and handlers here.
		/// </summary>
		protected override void OnNavigatedFrom(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
		{
			base.OnNavigatedFrom(e);

			// Stop receiving notifications for volume app bar button
			ACProxy.IsSynchronizingValues = false;

			AudioController.Default.CurrentTrackFailed -= AudioController_CurrentTrackFailed;
			AudioController.Default.StatusChanged -= AudioController_StatusChanged;

			Windows.Media.PlayTo.PlayToManager.GetForCurrentView().SourceRequested -= MainPage_SourceRequested;
		}

		/// <summary>
		/// Like OnNavigatedTo(), but runs after the whole UI has already been prepared.
		/// Place any loading code here that requires the UI to be completely loaded.
		/// </summary>
		private async void page_Loaded(object sender, RoutedEventArgs e)
		{
			// Initialize the controller if necessary
			if (AudioController.Default.Status == AudioControllerStatus.NotReady)
			{
				DependencyObject rootGrid = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(Window.Current.Content, 0);
				MediaElement rootMediaElement = (MediaElement)Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(rootGrid, 0);
				AudioController.Default.Ready(rootMediaElement);
			}

			// Set initial animation state; initializes the FadeXXXThemeAnimations
			VisualStateManager.GoToState(this, "NoOverlay", false);

			Type pageMode = typeof(Album);
			double scroll = 0d;
			ITrackContainer navigationContainer = null;
			Track navigationItem = null;

			if (_navigationParameter.HasValue && LibraryItemToken.VerifyToken(_navigationParameter.Value))
			{
				var navigationParameter = LibraryItemToken.GetItem(_navigationParameter.Value);

				// If there is a navigationParameter, use it, and ignore the pageState!
				if (navigationParameter is Album)
				{
					pageMode = typeof(Album);
					navigationContainer = navigationParameter as ITrackContainer;
				}
				else if (navigationParameter is Playlist)
				{
					pageMode = typeof(Playlist);
					navigationContainer = navigationParameter as ITrackContainer;
				}
				else if (navigationParameter is Track)
				{
					pageMode = typeof(Album);
					navigationItem = navigationParameter as Track;
					if (navigationItem != null)
					{
						navigationContainer = navigationItem.ContainingAlbum;
					}
				}
			}
			else if (_pageState != null)
			{
				// If no navigationParameter but pageState available, modify UI according to pageState
				if (_pageState.ContainsKey("PageMode"))
				{
					pageMode = Type.GetType((string)_pageState["PageMode"]);
				}

				if (_pageState.ContainsKey("AlbumScroll"))
				{
					scroll = (double)_pageState["AlbumScroll"];
				}
			}

			PageMode = pageMode;
			_pageState = null;
			_navigationParameter = null;

			if (navigationContainer != null)
			{
				VisualStateManager.GoToState(this, "AlbumGridViewPreparing", false);
				await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					// Delay scrolling after animation
					CurrentSelection = navigationContainer;
					albumGridView.ScrollIntoView(CurrentSelection);

					VisualStateManager.GoToState(this, "AlbumGridViewReady", true);

					if (navigationItem != null)
					{
						albumDetailListView.SelectedIndex = navigationContainer.IndexOf(navigationItem);
						albumDetailListView.ScrollIntoView(albumDetailListView.SelectedItem);
					}
				});

				IsAlbumOverlayShown = true;
			}
			else if (scroll > double.Epsilon) // Don't scroll if scroll is negative or less than Epsilon
			{
				VisualStateManager.GoToState(this, "AlbumGridViewPreparing", false);
				await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					// Delay scrolling after animation
					FindVisualChild<ScrollViewer>(albumGridView).ScrollToHorizontalOffset(scroll);

					VisualStateManager.GoToState(this, "AlbumGridViewReady", true);
				});
			}
		}

		#endregion Init/deinit code

		#region AudioController events

		private void AudioController_StatusChanged(object sender, AudioControllerStatusEventArgs e)
		{
			switch (e.Status)
			{
				case AudioControllerStatus.Inactive:
					VisualStateManager.GoToState(this, "NotPlaying", true);
					return;

				case AudioControllerStatus.Paused:
					VisualStateManager.GoToState(this, "Paused", true);
					return;

				case AudioControllerStatus.Playing:
					VisualStateManager.GoToState(this, "Playing", true);
					return;
			}
		}

		private void AudioController_CurrentTrackFailed(object sender, TrackEventArgs e)
		{
			ShowAlertFlyout(
				() => { },
				e.Track != null ? string.Format(LocalizationManager.GetString("MainPage/AudioError/Text_F"), e.Track.Title) : LocalizationManager.GetString("MainPage/AudioError/Text"),
				LocalizationManager.GetString("MainPage/AudioError/OKButton")
				);
		}

		#endregion AudioController events

		#region Properties

		private ITrackContainer CurrentSelection
		{
			get
			{
				return this.DefaultViewModel["CurrentSelection"] as ITrackContainer;
			}

			set
			{
				this.DefaultViewModel["CurrentSelection"] = value;
			}
		}

		private Type _PageMode = null;

		private Type PageMode
		{
			get
			{
				return _PageMode;
			}

			set
			{
				if (_PageMode != value)
				{
					if (value == typeof(Playlist))
					{
						_PageMode = typeof(Playlist);
						this.DefaultViewModel["CurrentFolder"] = TunesDataSource.Default.PlaylistsFlat;
						this.ShowAppBars();
					}
					else
					{
						_PageMode = typeof(Album);
						this.DefaultViewModel["CurrentFolder"] = TunesDataSource.Default.AlbumsFlat;
						this.ShowAppBars();
					}
				}
			}
		}

		private bool _IsAlbumOverlayShown = false;

		public bool IsAlbumOverlayShown
		{
			get
			{
				return _IsAlbumOverlayShown;
			}

			private set
			{
				if (_IsAlbumOverlayShown != value)
				{
					_IsAlbumOverlayShown = value;

					if (_IsAlbumOverlayShown)
					{
						IsQueueOverlayShown = false; // Only one at a time
						VisualStateManager.GoToState(this, "OverlayAlbum", true);
						this.ShowAppBars();
					}
					else
					{
						VisualStateManager.GoToState(this, "NoOverlay", true);
						albumDetailListView.SelectedItems.Clear();
						this.ShowAppBars();
					}
				}
			}
		}

		private bool _IsQueueOverlayShown = false;

		public bool IsQueueOverlayShown
		{
			get
			{
				return _IsQueueOverlayShown;
			}

			private set
			{
				if (_IsQueueOverlayShown != value)
				{
					_IsQueueOverlayShown = value;

					if (_IsQueueOverlayShown)
					{
						IsAlbumOverlayShown = false; // Only one at a time
						VisualStateManager.GoToState(this, "OverlayQueue", true);
						this.ShowAppBars();
					}
					else
					{
						VisualStateManager.GoToState(this, "NoOverlay", true);
						queueListView.SelectedItems.Clear();
						this.ShowAppBars();
					}
				}
			}
		}

		#endregion Properties

		#region Top app bar commands

		private void navigateOpenQueueButton_Click(object sender, RoutedEventArgs e)
		{
			if (IsQueueOverlayShown)
			{
				IsQueueOverlayShown = false;
			}
			else
			{
				IsQueueOverlayShown = true;
			}
		}

		private void navigateToAlbumsButton_Click(object sender, RoutedEventArgs e)
		{
			PageMode = typeof(Album);
		}

		private void navigateToPlaylistsButton_Click(object sender, RoutedEventArgs e)
		{
			PageMode = typeof(Playlist);
		}

		#endregion Top app bar commands

		#region Bottom app bar commands

		private void playButton_Click(object sender, object e)
		{
			if (AudioController.Default.Status == AudioControllerStatus.Inactive)
			{
				// Show a Play All menu
				ShowFlyout(_playMenu, ref _playFlyout, playButton, PlacementMode.Top);
			}
			else if (AudioController.Default.Status == AudioControllerStatus.Paused)
			{
				AudioController.Default.Play();
			}
			else
			{
				AudioController.Default.Pause();
			}
		}

		private void volumeButton_Click(object sender, RoutedEventArgs e)
		{
			ShowFlyout(volumeControls, ref _volumeFlyout, volumeButton, PlacementMode.Top);
		}

		private void skipBackButton_Click(object sender, RoutedEventArgs e)
		{
			if (AudioController.Default.Status == AudioControllerStatus.Playing || AudioController.Default.Status == AudioControllerStatus.Paused)
			{
				AudioController.Default.SkipBack();
			}
		}

		private void skipAheadButton_Click(object sender, RoutedEventArgs e)
		{
			if (AudioController.Default.Status == AudioControllerStatus.Playing || AudioController.Default.Status == AudioControllerStatus.Paused)
			{
				AudioController.Default.SkipAhead();
			}
		}

		private void clearQueueSelectionButton_Click(object sender, RoutedEventArgs e)
		{
			queueListView.SelectedItems.Clear();
		}

		private void clearAlbumSelectionButton_Click(object sender, RoutedEventArgs e)
		{
			albumDetailListView.SelectedItems.Clear();
		}

		private void addSelectionToQueueButton_Click(object sender, RoutedEventArgs e)
		{
			AudioController.Default.Add(albumDetailListView.SelectedItems
				.Cast<IndexedTrack>()
				.Select((p) => p.Track), true);

			if (AudioController.Default.Status == AudioControllerStatus.Inactive)
			{
				AudioController.Default.Play();
			}

			albumDetailListView.SelectedItems.Clear();
		}

		private void deleteSelectionFromQueueButton_Click(object sender, RoutedEventArgs e)
		{
			var itemsToDelete = queueListView.SelectedItems.Cast<UniqueTrack>().ToList();
			foreach (UniqueTrack t in itemsToDelete)
			{
				AudioController.Default.Remove(t);
			}
		}

		private void BottomAppBar_Loaded(object sender, RoutedEventArgs e)
		{
			// Get the app bar'path root Panel.
			Panel root = BottomAppBar.Content as Panel;
			if (root != null)
			{
				// Get the Panels that hold the controls.
				foreach (Panel panel in root.Children)
				{
					// Get each control and register for layout updates.
					foreach (UIElement child in panel.Children)
					{
						base.StartLayoutUpdates(child, new RoutedEventArgs());
					}
				}
			}
		}

		private void BottomAppBar_Unloaded(object sender, RoutedEventArgs e)
		{
			// Get the app bar'path root Panel.
			Panel root = BottomAppBar.Content as Panel;
			if (root != null)
			{
				// Get the Panels that hold the controls.
				foreach (Panel panel in root.Children)
				{
					// Get each control and unregister layout updates.
					foreach (UIElement child in panel.Children)
					{
						base.StopLayoutUpdates(child, new RoutedEventArgs());
					}
				}
			}
		}

		#endregion Bottom app bar commands

		#region UI: Album detail overlay

		private void albumGridView_ItemClick(object sender, ItemClickEventArgs e)
		{
			CurrentSelection = e.ClickedItem as ITrackContainer;
			IsAlbumOverlayShown = true;
		}

		private void albumDetailBlackOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			IsAlbumOverlayShown = false;
		}

		private void albumDetailListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.ShowAppBars();
		}

		private void albumDetailListView_ItemPlayButton_Click(object sender, RoutedEventArgs e)
		{
			FrameworkElement senderElement;
			if ((senderElement = sender as FrameworkElement) != null
				&& senderElement.Tag is IndexedTrack)
			{
				Track target = ((IndexedTrack)senderElement.Tag).Track;

				if (AudioController.Default.UserMutatedQueue)
				{
					// Prompt before replacement!
					ShowAlertFlyout(() => AudioController.Default.ReplaceAll(target, CurrentSelection, true),
						String.Format(LocalizationManager.GetString("Library/QueueWarning/Text_F"), target.Title),
						LocalizationManager.GetString("Library/QueueWarning/Confirm"),
						(UIElement)sender);
				}
				else
				{
					AudioController.Default.ReplaceAll(target, CurrentSelection, true);
				}
			}
		}

		private void playAllButton_Click(object sender, RoutedEventArgs e)
		{
			if (AudioController.Default.UserMutatedQueue)
			{
				// Prompt before replacement!
				ShowAlertFlyout(() => AudioController.Default.ReplaceAll(CurrentSelection, false, true),
					String.Format(LocalizationManager.GetString("Library/QueueWarning/Text_F"),
					CurrentSelection.Title),
					LocalizationManager.GetString("Library/QueueWarning/Confirm"),
					(UIElement)sender);
			}
			else
			{
				AudioController.Default.ReplaceAll(CurrentSelection, false, true);
			}
		}

		private void shuffleAllButton_Click(object sender, RoutedEventArgs e)
		{
			if (AudioController.Default.UserMutatedQueue)
			{
				// Prompt before replacement!
				ShowAlertFlyout(() => AudioController.Default.ReplaceAll(CurrentSelection, true, true),
					String.Format(LocalizationManager.GetString("Library/QueueWarning/Text_F"),
					CurrentSelection.Title), LocalizationManager.GetString("Library/QueueWarning/Confirm"),
					(UIElement)sender);
			}
			else
			{
				AudioController.Default.ReplaceAll(CurrentSelection, true, true);
			}
		}

		private void queueAllButton_Click(object sender, RoutedEventArgs e)
		{
			AudioController.Default.Add(CurrentSelection.TrackList, true);
		}

		#endregion UI: Album detail overlay

		#region UI: Queue overlay

		private void queueBlackOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			IsQueueOverlayShown = false;
		}

		private void queueListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.ShowAppBars();
		}

		private void queueListView_ItemPlayButton_Click(object sender, RoutedEventArgs e)
		{
			FrameworkElement senderElement;
			if ((senderElement = sender as FrameworkElement) != null
				&& senderElement.Tag is UniqueTrack)
			{
				AudioController.Default.Play((UniqueTrack)senderElement.Tag);
			}
		}

		#endregion UI: Queue overlay

		private async void MainPage_SourceRequested(Windows.Media.PlayTo.PlayToManager sender, Windows.Media.PlayTo.PlayToSourceRequestedEventArgs args)
		{
			await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
			{
				try
				{
					Windows.Media.PlayTo.PlayToSourceDeferral deferral = args.SourceRequest.GetDeferral();
					args.SourceRequest.SetSource(AudioController.Default.PlayToSource);
					deferral.Complete();
				}
				catch (Exception) { }
			});
		}

		/// <summary>
		/// Updates the UI from the code-behind to accomodate changes in the size of the window.
		/// </summary>
		/// <param name="path">The new size of the window.</param>
		/// <param name="viewState">The orientation of the window.</param>
		protected override void WindowSizeChanged(Size s, ApplicationViewState viewState)
		{
			if (viewState == ApplicationViewState.Snapped)
			{
				IsQueueOverlayShown = true;
			}
			else
			{
				IsQueueOverlayShown = false;
			}
		}

		private void ShowFlyout(FrameworkElement content, ref Flyout flyout, UIElement placementTarget, PlacementMode placementMode, Thickness hostMargin = new Thickness())
		{
			if (flyout != null)
			{
				flyout.IsOpen = false;
				flyout.Content = null;
				flyout.Dispose();
			}

			var parentPanel = content.Parent as Panel;
			if (parentPanel != null)
			{
				parentPanel.Children.Remove(content);
			}

			flyout = new Flyout();
			flyout.Content = content;
			flyout.HostMargin = hostMargin;
			flyout.Placement = placementMode;
			flyout.PlacementTarget = placementTarget;

			if (ApplicationView.Value == ApplicationViewState.Snapped)
			{
				flyout.MaxWidth = mainGrid.ActualWidth;
			}

			flyout.IsOpen = true;
		}

		/// <summary>
		/// Shows an alert flyout.
		/// </summary>
		/// <param name="action">The action to perform when the flyout's button is clicked.</param>
		/// <param name="alertMessage">The text to display in the flyout.</param>
		/// <param name="alertButtonMessage">The text to display in the flyout's button.</param>
		/// <param name="target">The object above which the flyout will be shown. If null, the flyout will be shown at the bottom of the screen.</param>
		private void ShowAlertFlyout(Action action, string alertMessage, string alertButtonMessage, UIElement target = null)
		{
			if (_alertHandler != null)
			{
				alertButton.Click -= _alertHandler;
			}

			alertText.Text = alertMessage;
			alertButtonText.Text = alertButtonMessage;

			UIElement actualTarget;
			if (target != null)
			{
				actualTarget = target;
			}
			else if (BottomAppBar.IsOpen)
			{
				actualTarget = BottomAppBar;
			}
			else
			{
				actualTarget = hiddenBottomEdge;
			}

			ShowFlyout(alertControls, ref _alertFlyout, actualTarget, PlacementMode.Top, new Thickness(10d, 0d, 10d, 0d));

			_alertHandler = (sender, e) => { _alertFlyout.IsOpen = false; action(); };
			alertButton.Click += _alertHandler;
		}

		/// <summary>
		/// Shows or hides the app bars and selection controls depending on the UI state.
		/// </summary>
		private void ShowAppBars()
		{
			if (BottomAppBar == null || TopAppBar == null)
			{
				return;
			}
			else if (IsAlbumOverlayShown && albumDetailListView != null && albumDetailListView.SelectedItems.Count != 0)
			{
				VisualStateManager.GoToState(this, "SelectionAlbum", true);

				BottomAppBar.IsSticky = true;
				BottomAppBar.IsOpen = true;
			}
			else if (IsQueueOverlayShown && queueListView != null && queueListView.SelectedItems.Count != 0)
			{
				VisualStateManager.GoToState(this, "SelectionQueue", true);

				BottomAppBar.IsSticky = true;
				BottomAppBar.IsOpen = true;
			}
			else
			{
				VisualStateManager.GoToState(this, "NoSelection", true);

				BottomAppBar.IsSticky = false;
				BottomAppBar.IsOpen = false;
				TopAppBar.IsOpen = false;
			}
		}
	}
}