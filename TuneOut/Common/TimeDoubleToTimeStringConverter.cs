using System;
using Windows.UI.Xaml.Data;

namespace TuneOut.Common
{
	internal class TimeDoubleToTimeStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			double d = value is double ? (double)value : 0d;
			TimeSpan time = TimeSpan.FromMilliseconds(d);

			int hoursRaw = time.Hours + time.Days * 24;
			string hours = hoursRaw == 0 ? string.Empty : hoursRaw.ToString() + ":";
			string mins = hoursRaw == 0 ? time.ToString("m\\:") : time.ToString("mm\\:");
			string secs = time.ToString("ss");

			return hours + mins + secs;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			string str = value as string;

			if (string.IsNullOrEmpty(str))
			{
				return 0d;
			}
			else
			{
				return TimeSpan.Parse(str).TotalMilliseconds;
			}
		}
	}
}