using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TuneOut.AppData;
using TuneOut.Audio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Search Contract item template is documented at http://go.microsoft.com/fwlink/?LinkId=234240

namespace TuneOut
{
	/// <summary>
	/// This page displays search results when a global search is directed to this application.
	/// </summary>
	public sealed partial class SearchResultsPage : TuneOut.Common.LayoutAwarePage
	{
		public Dictionary<Type, KeyValuePair<string, IEnumerable<ILibraryItem>>> Results { get; set; }

		public SearchResultsPage()
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
			var queryText = navigationParameter as String;

			// TODO: Application-specific searching logic.  The search process is responsible for
			//       creating a list of user-selectable result categories:
			//
			//       filterList.Add(new Filter("<filter name>", <result count>));
			//
			//       Only the first filter, typically "All", should pass true as a third argument in
			//       order to start in an active state.  Results for the active filter are provided
			//       in Filter_SelectionChanged below.

			var filterList = new List<Filter>();
			Results = new Dictionary<Type, KeyValuePair<string, IEnumerable<ILibraryItem>>>();
			if (string.IsNullOrEmpty(queryText))
			{
				Results.Add(typeof(Album),
					new KeyValuePair<string, IEnumerable<ILibraryItem>>(LocalizationManager.GetString("Items/Album/Description"), Enumerable.Empty<ILibraryItem>()));
				Results.Add(typeof(Playlist),
					new KeyValuePair<string, IEnumerable<ILibraryItem>>(LocalizationManager.GetString("Items/Playlist/Description"), Enumerable.Empty<ILibraryItem>()));
				Results.Add(typeof(Track),
					new KeyValuePair<string, IEnumerable<ILibraryItem>>(LocalizationManager.GetString("Items/Song/Description"), Enumerable.Empty<ILibraryItem>()));
			}
			else if (TunesDataSource.IsLoaded)
			{
				Results.Add(typeof(Album),
					new KeyValuePair<string, IEnumerable<ILibraryItem>>(LocalizationManager.GetString("Items/Album/Description"), TunesDataSource.Default.GetSearchResults<Album>(queryText)));
				Results.Add(typeof(Playlist),
					new KeyValuePair<string, IEnumerable<ILibraryItem>>(LocalizationManager.GetString("Items/Playlist/Description"), TunesDataSource.Default.GetSearchResults<Playlist>(queryText)));
				Results.Add(typeof(Track),
					new KeyValuePair<string, IEnumerable<ILibraryItem>>(LocalizationManager.GetString("Items/Song/Description"), TunesDataSource.Default.GetSearchResults<Track>(queryText)));
			}

			int total = 0;
			foreach (var group in Results)
			{
				int count = group.Value.Value.Count();
				if (count > 0)
				{
					filterList.Add(new Filter(group.Value.Key, group.Key, count));
					total += count;
				}
			}
			filterList.Insert(0, new Filter(LocalizationManager.GetString("Items/All/Description"), null, total, true));

			// Communicate results through the view model
			this.DefaultViewModel["QueryText"] = '\u201c' + queryText + '\u201d';
			this.DefaultViewModel["Filters"] = filterList;
			this.DefaultViewModel["ShowFilters"] = filterList.Count > 1;

			// Enable type-to-search
			Windows.ApplicationModel.Search.SearchPane.GetForCurrentView().ShowOnKeyboardInput = true;
		}

		/// <summary>
		/// Invoked when a filter is selected using the ComboBox in snapped view state.
		/// </summary>
		/// <param name="sender">The ComboBox instance.</param>
		/// <param name="e">Event data describing how the selected filter was changed.</param>
		private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Determine what filter was selected
			var selectedFilter = e.AddedItems.FirstOrDefault() as Filter;
			if (selectedFilter != null)
			{
				// Mirror the results into the corresponding Filter object to allow the
				// RadioButton representation used when not snapped to reflect the change
				selectedFilter.Active = true;

				// TODO: Respond to the change in active filter by setting this.DefaultViewModel["Results"]
				//       to a collection of items with bindable Image, Title, Subtitle, and Description properties
				if (selectedFilter.Type == null)
				{
					var allResults = Results[typeof(Album)].Value
						.Concat(Results[typeof(Playlist)].Value)
						.Concat(Results[typeof(Track)].Value).ToList();
					this.DefaultViewModel["Results"] = allResults;
				}
				else if (Results.ContainsKey(selectedFilter.Type))
				{
					this.DefaultViewModel["Results"] = Results[selectedFilter.Type].Value.ToList();
				}

				// Ensure results are found
				object results;
				ICollection resultsCollection;
				if (this.DefaultViewModel.TryGetValue("Results", out results) &&
					(resultsCollection = results as ICollection) != null &&
					resultsCollection.Count != 0)
				{
					VisualStateManager.GoToState(this, "ResultsFound", true);
					return;
				}
			}

			// Display informational text when there are no search results.
			VisualStateManager.GoToState(this, "NoResultsFound", true);
		}

		/// <summary>
		/// Invoked when a filter is selected using a RadioButton when not snapped.
		/// </summary>
		/// <param name="sender">The selected RadioButton instance.</param>
		/// <param name="e">Event data describing how the RadioButton was selected.</param>
		private void Filter_Checked(object sender, RoutedEventArgs e)
		{
			// Mirror the change into the CollectionViewSource used by the corresponding ComboBox
			// to ensure that the change is reflected when snapped
			if (filtersViewSource.View != null)
			{
				var filter = (sender as FrameworkElement).DataContext;
				filtersViewSource.View.MoveCurrentTo(filter);
			}
		}

		private async void Results_ItemClick(object sender, ItemClickEventArgs e)
		{
			await App.Current.Navigate(typeof(MainPage), LibraryItemToken.GetSingleUseToken((ILibraryItem)e.ClickedItem));
		}

		protected async override void GoBack(object sender, RoutedEventArgs e)
		{
			// Use the navigation frame to return to the previous page
			if (this.Frame != null && this.Frame.CanGoBack)
			{
				this.Frame.GoBack();
			}
			else
			{
				if (TunesDataSource.IsLoaded)
				{
					await App.Current.Navigate(typeof(MainPage), null);
				}
				else
				{
					await App.Current.Navigate(typeof(FirstRunPage), null);
				}
			}
		}

		/// <summary>
		/// View model describing one of the filters available for viewing search results.
		/// </summary>
		private sealed class Filter : TuneOut.Common.BindableBase
		{
			private String _name;
			private Type _type;
			private int _count;
			private bool _active;

			public Filter(String name, Type type, int count, bool active = false)
			{
				this.Name = name;
				this.Type = type;
				this.Count = count;
				this.Active = active;
			}

			public override String ToString()
			{
				return Description;
			}

			public String Name
			{
				get { return _name; }
				set { if (this.SetProperty(ref _name, value)) this.OnPropertyChanged("Description"); }
			}

			public Type Type
			{
				get { return _type; }
				set { this.SetProperty(ref _type, value); }
			}

			public int Count
			{
				get { return _count; }
				set { if (this.SetProperty(ref _count, value)) this.OnPropertyChanged("Description"); }
			}

			public bool Active
			{
				get { return _active; }
				set { this.SetProperty(ref _active, value); }
			}

			public String Description
			{
				get { return String.Format("{0} ({1})", _name, _count); }
			}
		}
	}
}