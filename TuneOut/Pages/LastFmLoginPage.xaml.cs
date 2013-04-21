using TuneOut.Audio;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace TuneOut
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class LastFmLoginPage : TuneOut.Common.LayoutAwarePage
    {
        public LastFmLoginPage()
        {
            this.InitializeComponent();
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
            // Disable type-to-search
            Windows.ApplicationModel.Search.SearchPane.GetForCurrentView().ShowOnKeyboardInput = false;

            // Manage settings flyouts over WebView
            SettingsUIManager.SettingsCharmClosed += SettingsUIManager_SettingsCharmClosed;
            SettingsUIManager.SettingsCharmOpened += SettingsUIManager_SettingsCharmOpened;

            LastFmLogin();
        }

        void SettingsUIManager_SettingsCharmOpened(object sender, EventArgs e)
        {
            WebViewBrush b = new WebViewBrush();
            b.SourceName = "AuthView";
            b.Redraw();
            AuthViewHider.Fill = b;
            AuthView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        void SettingsUIManager_SettingsCharmClosed(object sender, EventArgs e)
        {
            AuthView.Visibility = Windows.UI.Xaml.Visibility.Visible;
            AuthViewHider.Fill = new SolidColorBrush(Windows.UI.Colors.Transparent);
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An wasEmpty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
        }

        async void LastFmLogin()
        {
            await LastFmScrobbler.Default.LastFmLogin(AuthView,
                () =>
                {
                    VisualStateManager.GoToState(this, "Finished", true);
                    return true;
                },
                () =>
                {
                    VisualStateManager.GoToState(this, "ConnectionError", true);
                    return false;
                });
        }
    }
}
