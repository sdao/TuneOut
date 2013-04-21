using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuneOut.AppData
{
    /// <summary>
    /// Contains Last.fm API constants that should not be revealed.
    /// This version of the class will not be compiled.
    /// It contains versions of the constants where the API key and shared secret have been blanked out with
    /// [*INSERT API KEY HERE*] and [*INSERT SHARED SECRET HERE*].
    /// </summary>
    internal static class LastFmApiSecrets
    {
        internal const string LASTFMAPI_ALBUMGETINFO = "http://ws.audioscrobbler.com/2.0/?method=album.getinfo&api_key=[*INSERT API KEY HERE*]&artist={0}&album={1}&autocorrect=1&format=json";
        internal const string LASTFMAPI_GET_TOKEN = "http://ws.audioscrobbler.com/2.0/?method=auth.gettoken&api_key=[*INSERT API KEY HERE*]&format=json";
        internal const string LASTFMAPI_GET_SESSION_FORMAT = "http://ws.audioscrobbler.com/2.0/?method=auth.getsession&api_key=[*INSERT API KEY HERE*]&api_sig={1}&token={0}&format=json";
        internal const string LASTFMAPI_GET_SESSION_SIGN_FORMAT = "api_key[*INSERT API KEY HERE*]methodauth.getsessiontoken{0}[*INSERT SHARED SECRET HERE*]";
        internal const string LASTFMAPI_AUTH_URL_FORMAT = "http://www.last.fm/api/auth/?api_key=[*INSERT API KEY HERE*]&token={0}";
        internal const string LASTFMAPI_AUTH_SUCCESS_URL = "/api/grantaccess";
        internal const string LASTFMAPI_POST = "http://ws.audioscrobbler.com/2.0/";
        internal const string LASTFMAPI_TRACK_SCROBBLE_FORMAT = "method=track.scrobble&api_key=[*INSERT API KEY HERE*]&api_sig={5}&artist={0}&track={1}&timestamp={2}&album={3}&sk={4}";
        internal const string LASTFMAPI_TRACK_SCROBBLE_SIGN_FORMAT = "album{3}api_key[*INSERT API KEY HERE*]artist{0}methodtrack.scrobblesk{4}timestamp{2}track{1}[*INSERT SHARED SECRET HERE*]";
        internal const string LASTFMAPI_TRACK_UPDATENOWPLAYING_FORMAT = "method=track.updatenowplaying&api_key=[*INSERT API KEY HERE*]&api_sig={4}&artist={0}&track={1}&album={2}&sk={3}";
        internal const string LASTFMAPI_TRACK_UPDATENOWPLAYING_SIGN_FORMAT = "album{2}api_key[*INSERT API KEY HERE*]artist{0}methodtrack.updatenowplayingsk{3}track{1}[*INSERT SHARED SECRET HERE*]";
    }
}