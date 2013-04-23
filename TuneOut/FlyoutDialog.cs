using Callisto.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace TuneOut
{
	class FlyoutDialog : IDisposable
	{
		private readonly object _showAsyncLock = new object();
		private readonly List<IUICommand> _commands = new List<IUICommand>();

		private bool _open = false;

		private Flyout _flyout = null;
		private Panel _flyoutContainer = null;

		public void Dispose()
		{
			_flyout.DisposeIfNonNull();

			GC.SuppressFinalize(this);
		}

		~FlyoutDialog()
		{
			Dispose();
		}

		public FrameworkElement Content
		{
			get;
			set;
		}

		public IList<IUICommand> Commands
		{
			get
			{
				return _commands;
			}
		}

		public void Show(UIElement target, PlacementMode placement, Thickness internalPadding = new Thickness())
		{
			lock (_showAsyncLock)
			{
				if (_open)
				{
					// Must close previous one before opening a new one.
					DisposeFlyout();
				}

				_open = true;

				// Ensure Content is unparented, just in case!
				Panel contentParent = Content.Parent as Panel;
				if (contentParent != null)
				{
					contentParent.Children.Remove(Content);
				}

				// Create action buttons
				StackPanel flyoutButtonContainer = new StackPanel()
				{
					Orientation = Orientation.Horizontal,
					HorizontalAlignment = HorizontalAlignment.Right
				};
				foreach (IUICommand cmd in _commands)
				{
					Button button = new Button();
					button.Content = new TextBlock() { Text = cmd.Label };
					button.Tag = cmd;
					button.Style = Application.Current.Resources["FlyoutActionButton"] as Style;
					button.Click += button_Click;

					flyoutButtonContainer.Children.Add(button);
				}

				// Create outer container
				_flyoutContainer = new StackPanel()
				{
					Orientation = Orientation.Vertical,
					Margin = internalPadding
				};

				_flyoutContainer.Children.Add(Content);
				_flyoutContainer.Children.Add(flyoutButtonContainer);

				// Create flyout
				_flyout = new Flyout()
				{
					Content = _flyoutContainer,
					PlacementTarget = target,
					Placement = placement
				};

				_flyout.Closed += _flyout_Closed;
				_flyout.IsOpen = true;
			}
		}

		void _flyout_Closed(object sender, object e)
		{
			if (_open) // Prevent flyout from being disposed if that has already happened in click handler
			{
				DisposeFlyout();
			}
		}

		void button_Click(object sender, RoutedEventArgs e)
		{
			var senderButton = sender as Button;
			var command = senderButton.Tag as IUICommand;
			command.Invoked(command);

			if (_open)
			{
				DisposeFlyout();
			}
		}

		private void DisposeFlyout()
		{
			_flyout.IsOpen = false;
			_flyout.Content = null;

			_flyoutContainer.Children.Remove(Content);
			_flyoutContainer = null;

			_flyout.Dispose();
			_flyout = null;

			_open = false;
		}
	}
}
