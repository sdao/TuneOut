using TuneOut.AppData;
using System;

namespace TuneOut
{
    static class SettingsUIManager
    {
        public static event EventHandler SettingsCharmOpened;
        public static event EventHandler SettingsCharmClosed;

        public static void LoadSettingsCharm()
        {
            Callisto.Controls.SettingsManagement.AppSettings.Current.AddCommand<SettingsLibrary>(LocalizationManager.GetString("SettingsCharm/Options"));
            Callisto.Controls.SettingsManagement.AppSettings.Current.AddCommand<SettingsLegal>(LocalizationManager.GetString("SettingsCharm/Legal"));
        }

        static bool _SettingsCharmOpened = false;
        public static bool SettingsCharmOpen
        {
            get
            {
                return _SettingsCharmOpened;
            }

            set
            {
                if (_SettingsCharmOpened != value)
                {
                    _SettingsCharmOpened = value;

                    if (_SettingsCharmOpened == true && SettingsCharmOpened != null)
                    {
                        SettingsCharmOpened(null, new EventArgs());
                    }

                    if (_SettingsCharmOpened == false && SettingsCharmClosed != null)
                    {
                        SettingsCharmClosed(null, new EventArgs());
                    }
                }
            }
        }
    }
}
