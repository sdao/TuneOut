using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TuneOut.Common
{
	/// <summary>
	/// Value converter that translates true to <see cref="Visibility.Visible"/> and false to
	/// <see cref="Visibility.Collapsed"/>. Sending a non-null ConverterParameter will
	/// invert the behavior.
	/// </summary>
	public sealed class BooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (parameter != null)
			{
				return (value is bool && (bool)value) ? Visibility.Collapsed : Visibility.Visible;
			}
			else
			{
				return (value is bool && (bool)value) ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			if (parameter != null)
			{
				return value is Visibility && (Visibility)value == Visibility.Collapsed;
			}
			else
			{
				return value is Visibility && (Visibility)value == Visibility.Visible;
			}
		}
	}
}