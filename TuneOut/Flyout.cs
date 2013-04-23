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
	/// A wrapper for <seealso cref="Callisto.Controls.Flyout"/> which facilitates using content from an XML file,
	/// and which enforces the one-popup-at-a-time invariant.
	/// </summary>
	class Flyout : IDisposable
	{
		private static Flyout __currentOpenFlyout = null;

		private readonly object _showAsyncLock = new object();

		private Callisto.Controls.Flyout _flyout = null;

		private bool _isOpen = false;

		/// <summary>
		/// Creates a new flyout with the specified content.
		/// </summary>
		/// <param name="content"></param>
		public Flyout(FrameworkElement content)
		{
			Content = content;
		}

		public void Dispose()
		{
			this.DisposeFlyout();
			GC.SuppressFinalize(this);
		}

		~Flyout()
		{
			Dispose();
		}

		/// <summary>
		/// Gets or sets the content to display in the flyout.
		/// </summary>
		protected FrameworkElement Content
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets whether the flyout is the current open flyout.
		/// </summary>
		protected bool IsOpen
		{
			get
			{
				return _isOpen;
			}

			private set
			{
				_isOpen = value;

				if (_isOpen)
				{
					__currentOpenFlyout = this;
				}
				else if (__currentOpenFlyout == this)
				{
					__currentOpenFlyout = null;
				}
			}
		}

		/// <summary>
		/// Shows a flyout with the current <seealso cref="Content"/>.
		/// </summary>
		/// <param name="target">The object near which to show the flyout.</param>
		/// <param name="placement">The direction from the <paramref name="target"/> to show the flyout.</param>
		/// <param name="maxWidth">The maximum width of the flyout. If null, automatically determines a feasible width.</param>
		public virtual void Show(UIElement target, PlacementMode placement, double? maxWidth)
		{
			lock (_showAsyncLock)
			{
				if (__currentOpenFlyout != null)
				{
					// Must close previous one before opening a new one.
					__currentOpenFlyout.DisposeFlyout();
				}

				IsOpen = true;

				// Deconnect content
				var contentParent = Content.Parent as Panel;
				if (contentParent != null)
				{
					contentParent.Children.Remove(Content);
				}

				// Create flyout
				_flyout = new Callisto.Controls.Flyout()
				{
					Content = this.Content,
					PlacementTarget = target,
					Placement = placement
				};

				if (maxWidth.HasValue)
				{
					_flyout.MaxWidth = maxWidth.Value;
				}

				_flyout.Closed += _flyout_Closed;
				_flyout.IsOpen = true;
			}
		}

		void _flyout_Closed(object sender, object e)
		{
			this.DisposeFlyout();
		}

		/// <summary>
		/// Closes the current internal <seealso cref="Callisto.Settings.Flyout"/> and releases its resources.
		/// </summary>
		protected virtual void DisposeFlyout()
		{
			if (IsOpen)
			{
				// Set the all-important flag
				IsOpen = false;

				// Close the flyout and dispose it
				_flyout.IsOpen = false;
				_flyout.Content = null;
				_flyout.Dispose();
				_flyout = null;
			}
		}
	}
}
