using Callisto.Controls;
using System;
using TuneOut.AppData;
using TuneOut.Audio;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace TuneOut
{
	public sealed partial class SettingsLibrary : UserControl
	{
		public SettingsLibrary()
		{
			this.InitializeComponent();
			this.Loaded += (sender, e) =>
			{
				SettingsUIManager.SettingsCharmOpen = true;
				UpdateDisplay();
			};
			this.Unloaded += (sender, e) =>
			{
				SettingsUIManager.SettingsCharmOpen = false;
			};
		}

		private async void ResetLibraryButton_Click(object sender, RoutedEventArgs e)
		{
			var confirmDialog = new Windows.UI.Popups.MessageDialog(LocalizationManager.GetString("Settings/Library/ResetWarning/Text"), LocalizationManager.GetString("Settings/Library/ResetWarning/Confirm"));
			confirmDialog.Commands.Add(new Windows.UI.Popups.UICommand(LocalizationManager.GetString("Settings/Library/ResetWarning/Title"), async (cmd) =>
			{
				AudioController.Default.Stop();
				TunesDataSource.Reset();
				await App.Current.Navigate(typeof(FirstRunPage), null);
			}));
			confirmDialog.Commands.Add(new Windows.UI.Popups.UICommand(LocalizationManager.GetString("Settings/Library/ResetWarning/Cancel")));
			confirmDialog.DefaultCommandIndex = 0;
			confirmDialog.CancelCommandIndex = 1;

			await confirmDialog.ShowAsync();
		}

		private async void LastFmButton_Click(object sender, RoutedEventArgs e)
		{
			((SettingsFlyout)this.Parent).IsOpen = false;
			await App.Current.Navigate(typeof(LastFmLoginPage), null, Windows.ApplicationModel.Activation.ApplicationExecutionState.Running, NavigationReplacementMode.ReplaceIfDifferent, false);
		}

		private void LastFmLogoutButton_Click(object sender, RoutedEventArgs e)
		{
			LastFmScrobbler.Default.LogOut();
			UpdateDisplay();
		}

		private async void UpdateDisplay()
		{
			if (LastFmScrobbler.Default.IsLoggedIn)
			{
				LastFmStatus.Text = string.Format(LocalizationManager.GetString("Settings/LastFm/LoggedInNote_F"), LastFmScrobbler.Default.Name);
				VisualStateManager.GoToState(this, "LoggedIn", false);
			}
			else
			{
				VisualStateManager.GoToState(this, "LoggedOut", false);
			}

			if (TuneOut.AppData.Settings.GetLibraryLocationStatus())
			{
				StorageFolder folder = await TuneOut.AppData.Settings.GetLibraryLocation();
				LibraryStatus.Text = String.Format(LocalizationManager.GetString("Settings/Library/Linked_F"), folder.Path);
			}

			ForceDownloadSwitch.IsOn = Settings.IsInternetPolicyIgnored;
		}

		private void ForceDownloadSwitch_Toggled(object sender, RoutedEventArgs e)
		{
			Settings.IsInternetPolicyIgnored = ForceDownloadSwitch.IsOn;
		}
	}
}