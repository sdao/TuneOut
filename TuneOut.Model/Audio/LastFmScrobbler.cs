using TuneOut.AppData;
using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Security.Credentials;

namespace TuneOut.Audio
{
    /// <summary>
    /// Manages Last.fm user credentials and scrobbling functionality. Singleton.
    /// </summary>
    public class LastFmScrobbler
    {
        private const string PASSWORD_STORE = "last.fm";
        private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static readonly LastFmScrobbler _Default = new LastFmScrobbler();

        /// <summary>
        /// Gets the default singleton instance.
        /// </summary>
        public static LastFmScrobbler Default
        {
            get
            {
                Contract.Ensures(Contract.Result<LastFmScrobbler>() != null);
                return _Default;
            }
        }

        string _session = null;
        string _username = null;

        /// <summary>
        /// Creates a new LastFmScrobbler.
        /// </summary>
        private LastFmScrobbler() {
            try
            {
                PasswordVault vault = new PasswordVault();
                PasswordCredential cred = vault.FindAllByResource(PASSWORD_STORE).FirstOrDefault();
                if (cred != null)
                {
                    _username = cred.UserName;
                    _session = vault.Retrieve(PASSWORD_STORE, _username).Password;
                }
            }
            catch (Exception) { }
        }
        
        /// <summary>
        /// Present the Last.fm authentication UI and attempt to obtain a session key.
        /// </summary>
        /// <param name="authView">A WebView to display the Last.fm authentication page.</param>
        /// <param name="onSuccess">A callback for when the authentication is successful.</param>
        /// <param name="onFailure">A callback for when the authentication fails.</param>
        /// <returns>An async Task.</returns>
        public async Task LastFmLogin(Windows.UI.Xaml.Controls.WebView authView, Func<bool> onSuccess, Func<bool> onFailure)
        {
            Contract.Requires(authView != null);
            Contract.Requires(onSuccess != null);
            Contract.Requires(onFailure != null);

            try
            {
                HttpClient client = new HttpClient();

                HttpResponseMessage getTokenMessage = await client.GetAsync(LastFmApiSecrets.LASTFMAPI_GET_TOKEN);
                string tokenResponse = await getTokenMessage.Content.ReadAsStringAsync();

                string token = JsonObject
                    .Parse(tokenResponse)
                    .GetNamedString("token");

                string authUri = string.Format(LastFmApiSecrets.LASTFMAPI_AUTH_URL_FORMAT, token);
                authView.LoadCompleted += async (sender, e) =>
                {
                    try
                    {
                        if (e.Uri.LocalPath.ToString().ToLowerInvariant() == LastFmApiSecrets.LASTFMAPI_AUTH_SUCCESS_URL)
                        {
                            string getSessionSignature = string.Format(LastFmApiSecrets.LASTFMAPI_GET_SESSION_SIGN_FORMAT, token);
                            string getSessionSignatureMd5 = getSessionSignature.GetMd5Hash();
                            string getSession = string.Format(LastFmApiSecrets.LASTFMAPI_GET_SESSION_FORMAT, token, getSessionSignatureMd5);
                            HttpResponseMessage getSessionMessage = await client.GetAsync(getSession);
                            string sessionResponse = await getSessionMessage.Content.ReadAsStringAsync();

                            JsonObject session = JsonObject
                                .Parse(sessionResponse)
                                .GetNamedObject("session");

                            LogIn(session.GetNamedString("name"), session.GetNamedString("key"));

                            onSuccess();
                        }
                    }
                    catch (Exception)
                    {
                        // Failure after user interaction
                        onFailure();
                    }
                };
                authView.NavigationFailed += (sender, e) => {
                    // WebView navigation failure
                    onFailure();
                };
                authView.Navigate(new Uri(authUri));
            }
            catch (Exception)
            {
                // Failure before user interaction
                onFailure();
            }
        }

        /// <summary>
        /// Saves the login credentials for Last.fm.
        /// </summary>
        /// <param name="name">Last.fm username.</param>
        /// <param name="key">Last.fm session key.</param>
        private void LogIn(string name, string key)
        {
            Contract.Requires(!string.IsNullOrEmpty(name));
            Contract.Requires(!string.IsNullOrEmpty(key));

            _session = key;
            _username = name;

            if (_AudioController != null)
            {
                _AudioController.CurrentTrackChanged += _AudioController_CurrentTrackChanged;
            }

            PasswordVault vault = new PasswordVault();
            PasswordCredential c = new PasswordCredential(PASSWORD_STORE, name, key);
            vault.Add(c);
        }

        /// <summary>
        /// Removes all login credentials for Last.fm.
        /// </summary>
        public void LogOut()
        {
            _username = null;
            _session = null;

            if (_AudioController != null)
            {
                _AudioController.CurrentTrackChanged -= _AudioController_CurrentTrackChanged;
            }

            try
            {
                PasswordVault vault = new PasswordVault();
                PasswordCredential cred = vault.FindAllByResource(PASSWORD_STORE).FirstOrDefault();
                if (cred != null)
                {
                    vault.Remove(cred);
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Gets the current Last.fm session key.
        /// </summary>
        private string Session
        {
            get
            {
                return _session;
            }
        }

        /// <summary>
        /// Gets the logged-in Last.fm username.
        /// </summary>
        public string Name
        {
            get
            {
                return _username;
            }
        }

        /// <summary>
        /// Gets whether the user is logged in to Last.fm
        /// </summary>
        public bool IsLoggedIn
        {
            get
            {
                return _session != null;
            }
        }

        /// <summary>
        /// Sends a Now Playing message to Last.fm.
        /// </summary>
        /// <param name="t">The track that is now playing.</param>
        public void NowPlaying(Track t)
        {
            Contract.Requires(t != null);

            if (Session != null && NetworkStatusManager.Default.IsInternetAvailable)
            {
                IAsyncOperation<bool> asyncOp = NowPlayingAsync(t);
            }
        }

        /// <summary>
        /// Sends a Now Playing message to Last.fm.
        /// </summary>
        /// <param name="t">The track that is now playing.</param>
        /// <returns>An async Task.</returns>
        private IAsyncOperation<bool> NowPlayingAsync(Track t)
        {
            Contract.Requires(t != null);

            return Task.Run(async () =>
            {
                try
                {
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    string trackNowPlayingSignature = string.Format(LastFmApiSecrets.LASTFMAPI_TRACK_UPDATENOWPLAYING_SIGN_FORMAT,
                        t.Artist,
                        t.Title,
                        t.Album,
                        Session);
                    string trackNowPlayingSignatureMd5 = trackNowPlayingSignature.GetMd5Hash();
                    string trackNowPlaying = string.Format(LastFmApiSecrets.LASTFMAPI_TRACK_UPDATENOWPLAYING_FORMAT,
                        Uri.EscapeDataString(t.Artist),
                        Uri.EscapeDataString(t.Title),
                        Uri.EscapeDataString(t.Album),
                        Session,
                        trackNowPlayingSignatureMd5);

                    StringContent content = new StringContent(trackNowPlaying);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    HttpResponseMessage trackNowPlayingMessage = await client.PostAsync(LastFmApiSecrets.LASTFMAPI_POST, content);
                    // string nowPlayingResponse = await trackNowPlayingMessage.Content.ReadAsStringAsync();
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }).AsAsyncOperation();
        }

        /// <summary>
        /// Sends a Scrobble request to Last.fm.
        /// </summary>
        /// <param name="t">The track to scrobble.</param>
        /// <param name="time">The UTC time that the track started playing.</param>
        public void Scrobble(Track t, DateTime time)
        {
            Contract.Requires(t != null);

            if (Session != null && NetworkStatusManager.Default.IsInternetAvailable)
            {
                IAsyncOperation<bool> asyncOp = ScrobbleAsync(t, (int)time.Subtract(UNIX_EPOCH).TotalSeconds);
            }
        }

        /// <summary>
        /// Sends a Scrobble request to Last.fm.
        /// </summary>
        /// <param name="t">The track to scrobble.</param>
        /// <param name="timeStamp">The UNIX timestamp that the track started playing.</param>
        /// <returns>An async Task.</returns>
        private IAsyncOperation<bool> ScrobbleAsync(Track t, int timeStamp)
        {
            Contract.Requires(t != null);

            return Task.Run(async () =>
            {
                try
                {
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    string trackScrobbleSignature = string.Format(LastFmApiSecrets.LASTFMAPI_TRACK_SCROBBLE_SIGN_FORMAT,
                        t.Artist,
                        t.Title,
                        timeStamp,
                        t.Album,
                        Session);
                    string trackScrobbleSignatureMd5 = trackScrobbleSignature.GetMd5Hash();
                    string trackScrobble = string.Format(LastFmApiSecrets.LASTFMAPI_TRACK_SCROBBLE_FORMAT,
                        Uri.EscapeDataString(t.Artist),
                        Uri.EscapeDataString(t.Title),
                        timeStamp,
                        Uri.EscapeDataString(t.Album),
                        Session,
                        trackScrobbleSignatureMd5);

                    StringContent content = new StringContent(trackScrobble);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    HttpResponseMessage trackScrobbleMessage = await client.PostAsync(LastFmApiSecrets.LASTFMAPI_POST, content);
                    // string scrobbleResponse = await trackScrobbleMessage.Content.ReadAsStringAsync();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }).AsAsyncOperation();
        }

        AudioController _AudioController;
        /// <summary>
        /// Registers event handlers with an AudioController in order to scrobble.
        /// </summary>
        /// <param name="controller">The AudioController.</param>
        public void ConnectAudioController(AudioController controller)
        {
            Contract.Requires(controller != null);

            _AudioController = controller;

            if (IsLoggedIn)
            {
                _AudioController.CurrentTrackChanged += _AudioController_CurrentTrackChanged;
            }
        }

        void _AudioController_CurrentTrackChanged(object sender, TrackChangedEventArgs e)
        {
            if (e.Track != null && e.Track.TotalTime > new TimeSpan(0, 0, 30) &&
                (e.Position.Ticks > e.Track.TotalTime.Ticks / 2 || e.Position > new TimeSpan(0, 4, 0)))
            {
                Scrobble(e.Track, e.StartTimeUtc);
            }

            if (e.NewTrack != null)
            {
                NowPlaying(e.NewTrack);
            }
        }
    }
}
