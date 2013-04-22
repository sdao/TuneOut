using System;
using TuneOut.Audio;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=234227

namespace TuneOut
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	sealed partial class App : Application
	{
		private static string NAVIGATION_EMPTY = null;
		private static readonly Uri UNKNOWN_ARTWORK = new Uri("ms-appx:///Assets/UnknownMusic.png");
		private static readonly Uri PLAYLIST_ARTWORK = new Uri("ms-appx:///Assets/Playlist.png");

		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			this.InitializeComponent();
			this.Suspending += OnSuspending;

			// Set defaults now.
			AppData.Defaults.UnknownArtwork = UNKNOWN_ARTWORK;
			AppData.Defaults.PlaylistArtwork = PLAYLIST_ARTWORK;
		}

		public new static App Current
		{
			get
			{
				return (App)Application.Current;
			}
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used when the application is launched to open a specific file, to display
		/// search results, and so forth.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected async override void OnLaunched(LaunchActivatedEventArgs args)
		{
			bool hasData = await TunesDataSource.Load();
			if (hasData)
			{
				await Navigate(typeof(MainPage), args.Arguments, args.PreviousExecutionState, NavigationReplacementMode.NeverReplace, false);
			}
			else
			{
				await Navigate(typeof(FirstRunPage), args.Arguments, args.PreviousExecutionState, NavigationReplacementMode.ReplaceIfDifferent, false);
			}

			// Load settings charm
			SettingsUIManager.LoadSettingsCharm();
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the content
		/// of memory still intact.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();
			await AudioController.Default.SerializeAsync();
			await TuneOut.Common.SuspensionManager.SaveAsync();
			deferral.Complete();
		}

		/// <summary>
		/// Invoked when the application is activated to display search results.
		/// </summary>
		/// <param name="args">Details about the activation request.</param>
		protected async override void OnSearchActivated(Windows.ApplicationModel.Activation.SearchActivatedEventArgs args)
		{
			// TODO: Register the Windows.ApplicationModel.Search.SearchPane.GetForCurrentView().QuerySubmitted
			// event in OnWindowCreated to speed up searches once the application is already running
			// Reinitialize the app if a new instance was launched for search

			// Load data silently; if it is not available, just give up!
			await TunesDataSource.Load();

			await Navigate(typeof(SearchResultsPage), args.QueryText, args.PreviousExecutionState, NavigationReplacementMode.AlwaysReplace, false);

			// Load settings charm
			SettingsUIManager.LoadSettingsCharm();
		}

		/// <summary>
		/// Attempts to navigate to a specific page, loading from suspension settings if requested.
		/// </summary>
		/// <param name="pageType">The type of the page.</param>
		/// <param name="parameter">The parameter to pass to the page.</param>
		/// <param name="previousState">The previous execution state of the application. If the previous state was ApplicationExecutionState.Terminated, then the method will attempt to load suspension settings. </param>
		/// <param name="forceReplaceCurrent">Determines whether the current page is replaced never, always, or only if it is different.</param>
		/// <param name="clearHistory">Clears the history stack up to this point. Use this option if navigating to a page without a back button.</param>
		/// <returns>A Task object for async operations.</returns>
		public async System.Threading.Tasks.Task Navigate(Type pageType, object parameter, ApplicationExecutionState previousState = ApplicationExecutionState.Running, NavigationReplacementMode forceReplaceCurrent = NavigationReplacementMode.ReplaceIfDifferent, bool clearHistory = true)
		{
			Frame rootFrame = Window.Current.Content as Frame;

			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active
			if (rootFrame == null)
			{
				// Create a Frame to act as the navigation context and navigate to the first page
				// This Root Frame contains the media element!
				rootFrame = new Frame();
				rootFrame.Style = Resources["RootFrameStyle"] as Style;

				App.NAVIGATION_EMPTY = rootFrame.GetNavigationState(); // Save the wasEmpty navigation state
				TuneOut.Common.SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

				if (previousState == ApplicationExecutionState.Terminated)
				{
					// Restore the saved session state only when appropriate
					try
					{
						await TuneOut.Common.SuspensionManager.RestoreAsync();
					}
					catch (TuneOut.Common.SuspensionManagerException)
					{
						// Something went wrong restoring state.
						// Assume there is no state and continue
					}
				}

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}

			if (clearHistory && NAVIGATION_EMPTY != null)
			{
				rootFrame.SetNavigationState(NAVIGATION_EMPTY);
			}

			if (forceReplaceCurrent == NavigationReplacementMode.AlwaysReplace && !rootFrame.Navigate(pageType, parameter))
			{
				throw new Exception("Failed to navigate to page");
			}
			else if (forceReplaceCurrent == NavigationReplacementMode.ReplaceIfDifferent && rootFrame.CurrentSourcePageType != pageType && !rootFrame.Navigate(pageType, parameter))
			{
				throw new Exception("Failed to navigate to page");
			}
			else if (rootFrame.Content == null && !rootFrame.Navigate(pageType, parameter))
			{
				// When the navigation stack isn't restored navigate to the first page,
				// configuring the new page by passing required information as a navigation
				// parameter

				throw new Exception("Failed to create initial page");
			}

			// Ensure the current window is active
			Window.Current.Activate();
		}
	}

	public enum NavigationReplacementMode
	{
		/// <summary>
		/// If a page is already loaded, then do not navigate.
		/// </summary>
		NeverReplace,

		/// <summary>
		/// Always navigate regardless of whether a page was already loaded.
		/// </summary>
		AlwaysReplace,

		/// <summary>
		/// Navigate only if the current page type is different from the requested page type.
		/// </summary>
		ReplaceIfDifferent
	}
}