using System;
using System.Collections.Generic;
using TuneOut.Audio;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace TuneOut
{
	/// <summary>
	/// A basic page that provides characteristics common to most applications.
	/// </summary>
	public sealed partial class FirstRunPage : TuneOut.Common.LayoutAwarePage
	{
		public FirstRunPage()
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
			if (TuneOut.AppData.Settings.IsFirstRunComplete)
			{
				VisualStateManager.GoToState(this, "SecondRun", false);
			}
			else
			{
				VisualStateManager.GoToState(this, "FirstRun", false);
			}
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

		private async void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			MessageDialog errorDialog = null;

			if (this.EnsureUnsnapped())
			{
				FolderPicker folderPicker = new FolderPicker();
				folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
				folderPicker.FileTypeFilter.Add(".");

				StorageFolder folder = await folderPicker.PickSingleFolderAsync();
				if (folder != null)
				{
					VisualStateManager.GoToState(this, "Loading", true);

					// Reset everything just in case first.
					TunesDataSource.Reset(firstRunComplete: true);

					// Application now has read/write access to all content in the picked folder (including other sub-folder content)
					TuneOut.AppData.Settings.SetLibraryLocation(folder);

					bool hasData = await TunesDataSource.Load();
					if (hasData)
					{
						// Completed First-Run
						TuneOut.AppData.Settings.IsFirstRunComplete = true;

						// Go to MainPage
						await App.Current.Navigate(typeof(MainPage), null);
						return;
					}
					else
					{
						// Error: file problem.
						errorDialog = new MessageDialog(TuneOut.AppData.LocalizationManager.GetString("FirstRunPage/FileErrorDialog/Text"), TuneOut.AppData.LocalizationManager.GetString("FirstRunPage/FileErrorDialog/Title"));
					}
				}
				else
				{
					// User cancelled.
					return;
				}
			}
			else
			{
				// Error: cannot unsnap.
				errorDialog = new MessageDialog(TuneOut.AppData.LocalizationManager.GetString("LayoutAwarePage/UnsnapText"));
			}

			VisualStateManager.GoToState(this, "NotLoading", true);
			await errorDialog.ShowAsync();
		}
	}
}