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
	/// <summary>
	/// A wrapper for <seealso cref="Callisto.Controls.Flyout"/> for showing short informational messages.
	/// </summary>
	class FlyoutDialog : Flyout
	{
		private readonly List<IUICommand> _commands = new List<IUICommand>();

		/// <summary>
		/// Creates a new, empty flyout dialog.
		/// </summary>
		public FlyoutDialog()
			: base(null)
		{
		}

		public String Text
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

		/// <summary>
		/// Shows a flyout with the current <seealso cref="Text"/> and <seealso cref="Commands"/>.
		/// </summary>
		/// <param name="target">The object near which to show the flyout.</param>
		/// <param name="placement">The direction from the <paramref name="target"/> to show the flyout.</param>
		/// <param name="maxWidth">The maximum width of the flyout. If null, automatically determines a feasible width.</param>
		public override void Show(UIElement target, PlacementMode placement, double? maxWidth)
		{
			// Create text
			TextBlock textContainer = new TextBlock()
			{
				Text = this.Text,
				Style = Application.Current.Resources["FlyoutText"] as Style
			};

			// Create action buttons
			StackPanel flyoutButtonContainer = new StackPanel()
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right
			};

			foreach (IUICommand cmd in _commands)
			{
				Button button = new Button()
				{
					Content = new TextBlock() { Text = cmd.Label },
					Tag = cmd,
					Style = Application.Current.Resources["FlyoutActionButton"] as Style
				};

				button.Click += button_Click;

				flyoutButtonContainer.Children.Add(button);
			}

			// Create outer container
			var outerContainer = new StackPanel()
			{
				Orientation = Orientation.Vertical,
				Margin = new Thickness(10d)
			};

			outerContainer.Children.Add(textContainer);
			outerContainer.Children.Add(flyoutButtonContainer);

			Content = outerContainer;
			base.Show(target, placement, maxWidth);
		}

		void button_Click(object sender, RoutedEventArgs e)
		{
			var senderButton = sender as Button;
			var command = senderButton.Tag as IUICommand;
			command.Invoked(command);

			this.DisposeFlyout();
		}

		protected override void DisposeFlyout()
		{
			base.DisposeFlyout();
			Content = null;
		}
	}
}
