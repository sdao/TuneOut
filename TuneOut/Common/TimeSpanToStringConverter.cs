using System;
using Windows.UI.Xaml.Data;

namespace TuneOut.Common
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            TimeSpan time = (value as TimeSpan?).GetValueOrDefault();

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
                return TimeSpan.Zero;
            }
            else
            {
                return TimeSpan.Parse(str);
            }
        }
    }
}
