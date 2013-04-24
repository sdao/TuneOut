using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TuneOut.Common
{
	/// <summary>
	/// A converter that converts the integer zero to Visible, all other values to Collapsed, and vice versa.
	/// </summary>
	class ZeroToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return (value is int && (int)value == 0) ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			return value is Visibility && (Visibility)value == Visibility.Visible ? 0 : 1;
		}
	}
}
