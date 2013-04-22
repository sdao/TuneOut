using System;
using Windows.UI.Xaml.Data;

namespace TuneOut.Common
{
	public class TimeSpanToDoubleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			TimeSpan time = (value as TimeSpan?).GetValueOrDefault();
			return time.TotalMilliseconds;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			double d = value is double ? (double)value : 0d;
			return TimeSpan.FromMilliseconds(d);
		}
	}
}