using Callisto.Controls;
using TuneOut.AppData;
using TuneOut.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace TuneOut
{
    /// <summary>
    /// An wasEmpty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : TuneOut.Common.LayoutAwarePage, IDisposable
    {
        private const int QUEUE_DISPLAY_LIMIT = 8;

        Flyout _volumeFlyout;
        Flyout _playFlyout;
        Flyout _alertFlyout;

        Dictionary<string, object> _pageState;
        Guid? _navigationParameter;

        public MainPage()
        {
            Contract.Requires(TunesDataSource.IsLoaded);

            this.InitializeComponent();

            // Initialize volume controls
            HiddenControls.Children.Remove(VolumeControls);

            _volumeFlyout = new Flyout();
            _volumeFlyout.Content = VolumeControls;
            _volumeFlyout.Placement = PlacementMode.Top;
            _volumeFlyout.PlacementTarget = VolumeButton;
            _volumeFlyout.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;

            // Initialize Play All menu
            Menu menu = new Menu();

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

            menu.Items.Add(playAllItem);
            menu.Items.Add(shuffleItem);

            _playFlyout = new Flyout();
            _playFlyout.Content = menu;
            _playFlyout.Placement = PlacementMode.Top;
            _playFlyout.PlacementTarget = PlayButton;
            _playFlyout.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
        }

        public void Dispose()
        {
            _volumeFlyout.DisposeIfNonNull();
            _playFlyout.DisposeIfNonNull();
            _alertFlyout.DisposeIfNonNull();
            (this.Resources["ACProxy"] as IDisposable).DisposeIfNonNull();
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
            pageState["AlbumViewer.Scroll"] = FindVisualChild<ScrollViewer>(AlbumViewer).HorizontalOffset;
        }

        /// <summary>
        /// Set up UI and handlers here.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Start receiving notifications for volume app bar button
            var acProxy = this.Resources["ACProxy"] as AudioControllerProxy;
            if (acProxy != null)
            {
                acProxy.IsSynchronizingValues = true;
            }

            // Load the audio controller
            AudioController.Default.CurrentTrackFailed += Default_CurrentTrackFailed;
            AudioController.Default.StatusChanged += Default_StatusChanged;

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
            var abProxy = this.Resources["ACProxy"] as AudioControllerProxy;
            if (abProxy != null)
            {
                abProxy.IsSynchronizingValues = false;
            }

            AudioController.Default.CurrentTrackFailed -= Default_CurrentTrackFailed;
            AudioController.Default.StatusChanged -= Default_StatusChanged;

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

                if (_pageState.ContainsKey("AlbumViewer.Scroll"))
                {
                    scroll = (double)_pageState["AlbumViewer.Scroll"];
                }
            }

            PageMode = pageMode;
            _pageState = null;
            _navigationParameter = null;

            if (navigationContainer != null)
            {
                VisualStateManager.GoToState(this, "ViewerPreparing", false);
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    // Delay scrolling after animation
                    CurrentSelection = navigationContainer;
                    AlbumViewer.ScrollIntoView(CurrentSelection);

                    VisualStateManager.GoToState(this, "ViewerReady", true);

                    if (navigationItem != null)
                    {
                        SelectedAlbumTracks.SelectedIndex = navigationContainer.IndexOf(navigationItem);
                        SelectedAlbumTracks.ScrollIntoView(SelectedAlbumTracks.SelectedItem);
                    }
                });

                IsAlbumOverlayShown = true;
            }
            else if (scroll > double.Epsilon) // Don't scroll if scroll is negative or less than Epsilon
            {
                VisualStateManager.GoToState(this, "ViewerPreparing", false);
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    // Delay scrolling after animation
                    FindVisualChild<ScrollViewer>(AlbumViewer).ScrollToHorizontalOffset(scroll);

                    VisualStateManager.GoToState(this, "ViewerReady", true);
                });
            }
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

        /// <summary>
        /// Shows an alert flyout.
        /// </summary>
        /// <param name="action">The action to perform when the flyout's button is clicked.</param>
        /// <param name="alertText">The text to display in the flyout.</param>
        /// <param name="alertActionText">The text to display in the flyout's button.</param>
        /// <param name="target">The object above which the flyout will be shown. If null, the flyout will be shown at the bottom of the screen.</param>
        private void ShowAlertFlyout(Action action, string alertText, string alertActionText, UIElement target = null)
        {
            if (_alertFlyout != null)
            {
                _alertFlyout.IsOpen = false;
                _alertFlyout.Dispose();
            }

            _alertFlyout = new Flyout();

            TextBlock text = new TextBlock();
            text.Text = alertText;
            text.Style = (Style)(Application.Current.Resources["FlyoutText"]);

            TextBlock buttonText = new TextBlock();
            buttonText.Text = alertActionText;

            Button button = new Button();
            button.Content = buttonText;
            button.Click += (sender, e) => { _alertFlyout.IsOpen = false; action(); };
            button.Style = (Style)(Application.Current.Resources["FlyoutActionButton"]);

            StackPanel flyoutContents = new StackPanel();
            flyoutContents.Orientation = Orientation.Vertical;
            flyoutContents.Children.Add(text);
            flyoutContents.Children.Add(button);
            flyoutContents.Margin = new Thickness(10.0);

            _alertFlyout.Content = flyoutContents;
            _alertFlyout.HostMargin = new Thickness(10.0, 0.0, 10.0, 0.0);
            _alertFlyout.Placement = PlacementMode.Top;

            if (target != null)
            {
                _alertFlyout.PlacementTarget = target;
            }
            else if (BottomAppBar.IsOpen)
            {
                _alertFlyout.PlacementTarget = BottomAppBar;
            }
            else
            {
                _alertFlyout.PlacementTarget = HiddenBottomEdge;
            }

            if (ApplicationView.Value == ApplicationViewState.Snapped)
            {
                _alertFlyout.MaxWidth = MainGrid.ActualWidth;
            }

            _alertFlyout.IsOpen = true;
        }

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

        #region AudioController events

        void Default_StatusChanged(object sender, AudioControllerStatusEventArgs e)
        {
            switch (e.Status)
            {
                case AudioControllerStatus.Inactive:
                    VisualStateManager.GoToState(this, "NotPlaying", true);
                    return;
                case AudioControllerStatus.Paused:
                    VisualStateManager.GoToState(this, "PlayingPaused", true);
                    return;
                case AudioControllerStatus.Playing:
                    VisualStateManager.GoToState(this, "Playing", true);
                    return;
            }
        }

        void Default_CurrentTrackFailed(object sender, TrackEventArgs e)
        {
            ShowAlertFlyout(
                () => { },
                e.Track != null ? string.Format(LocalizationManager.GetString("MainPage/AudioError/Text_F"), e.Track.Title) : LocalizationManager.GetString("MainPage/AudioError/Text"),
                LocalizationManager.GetString("MainPage/AudioError/OKButton")
                );
        }

        #endregion

        #region Properties

        ITrackContainer CurrentSelection
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

        Type _PageMode = null;
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
                        BottomAppBar.IsOpen = false;
                        TopAppBar.IsOpen = false;
                    }
                    else
                    {
                        _PageMode = typeof(Album);
                        this.DefaultViewModel["CurrentFolder"] = TunesDataSource.Default.AlbumsFlat;
                        BottomAppBar.IsOpen = false;
                        TopAppBar.IsOpen = false;
                    }
                }
            }
        }

        bool _IsAlbumOverlayShown = false;
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
                        BottomAppBar.IsOpen = false;
                        TopAppBar.IsOpen = false;
                    }
                    else
                    {
                        VisualStateManager.GoToState(this, "NoOverlay", true);
                        BottomAppBar.IsOpen = false;
                        TopAppBar.IsOpen = false;
                        SelectedAlbumTracks.SelectedItems.Clear();
                    }
                }
            }
        }

        bool _IsQueueOverlayShown = false;
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
                        BottomAppBar.IsOpen = false;
                        TopAppBar.IsOpen = false;
                    }
                    else
                    {
                        VisualStateManager.GoToState(this, "NoOverlay", true);
                        BottomAppBar.IsOpen = false;
                        TopAppBar.IsOpen = false;
                        QueueTracks.SelectedItems.Clear();
                    }
                }
            }
        }

        #endregion

        #region App bar commands

        private void QueueButton_Click(object sender, RoutedEventArgs e)
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

        private void NavAlbums_Click(object sender, RoutedEventArgs e)
        {
            PageMode = typeof(Album);
        }

        private void NavPlaylists_Click(object sender, RoutedEventArgs e)
        {
            PageMode = typeof(Playlist);
        }

        private void PlayPauseButton_Pressed(object sender, object e)
        {
            if (AudioController.Default.Status == AudioControllerStatus.Inactive)
            {
                // Show a Play All menu
                _playFlyout.IsOpen = true;
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

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            _volumeFlyout.IsOpen = true;
        }

        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {
            if (AudioController.Default.Status == AudioControllerStatus.Playing || AudioController.Default.Status == AudioControllerStatus.Paused)
            {
                AudioController.Default.SkipBack();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (AudioController.Default.Status == AudioControllerStatus.Playing || AudioController.Default.Status == AudioControllerStatus.Paused)
            {
                AudioController.Default.SkipAhead();
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

        #endregion

        #region UI: Album overlay

        private void AlbumViewer_ItemClick(object sender, ItemClickEventArgs e)
        {
            CurrentSelection = e.ClickedItem as ITrackContainer;
            IsAlbumOverlayShown = true;
        }

        private void ClickCatcher_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            IsAlbumOverlayShown = false;
        }

        private void SelectedAlbumTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedAlbumTracks.SelectedItems.Count > 0)
            {
                VisualStateManager.GoToState(this, "SelectionAlbum", true);
                BottomAppBar.IsSticky = true;
                BottomAppBar.IsOpen = true;
            }
            else
            {
                VisualStateManager.GoToState(this, "NoSelection", true);
                BottomAppBar.IsSticky = false;
                BottomAppBar.IsOpen = false;
            }
        }

        private void ClearAlbumSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAlbumTracks.SelectedItems.Clear();
        }

        private void AddToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            AudioController.Default.Add(SelectedAlbumTracks.SelectedItems
                .Cast<IndexedTrack>()
                .Select((p) => p.Track), true);

            if (AudioController.Default.Status == AudioControllerStatus.Inactive)
            {
                AudioController.Default.Play();
            }

            SelectedAlbumTracks.SelectedItems.Clear();
        }

        private void SinglePlayButton_Click(object sender, RoutedEventArgs e)
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

        private void PlayWholeAlbumButton_Click(object sender, RoutedEventArgs e)
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

        private void ShuffleWholeAlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (AudioController.Default.UserMutatedQueue)
            {
                // Prompt before replacement!
                ShowAlertFlyout(() =>AudioController.Default.ReplaceAll(CurrentSelection, true, true),
                    String.Format(LocalizationManager.GetString("Library/QueueWarning/Text_F"),
                    CurrentSelection.Title), LocalizationManager.GetString("Library/QueueWarning/Confirm"),
                    (UIElement)sender);
            }
            else
            {
                AudioController.Default.ReplaceAll(CurrentSelection, true, true);
            }
        }

        private void QueueWholeAlbumButton_Click(object sender, RoutedEventArgs e)
        {
            AudioController.Default.Add(CurrentSelection.TrackList, true);
        }

        #endregion

        #region UI: Queue flyout

        private void QueueClickCatcher_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            IsQueueOverlayShown = false;
        }

        private void QueueTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QueueTracks.SelectedItems.Count > 0)
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
            }
        }

        private void ClearQueueSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            QueueTracks.SelectedItems.Clear();
        }

        private void CutQueueButton_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement senderElement;
            if ((senderElement = sender as FrameworkElement) != null
                && senderElement.Tag is UniqueTrack)
            {
                AudioController.Default.Play((UniqueTrack)senderElement.Tag);
            }
        }

        private void DeleteFromQueueButton_Click(object sender, RoutedEventArgs e)
        {
            var itemsToDelete = QueueTracks.SelectedItems.Cast<UniqueTrack>().ToList();
            foreach (UniqueTrack t in itemsToDelete)
            {
                AudioController.Default.Remove(t);
            }
        }

        #endregion
    }
}
