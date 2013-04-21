using System;

namespace TuneOut.Common
{
    public class StringFormatConverter : Windows.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string format = parameter as string;

            if (value == null || string.IsNullOrEmpty(format))
            {
                return null;
            }
            else
            {
                return string.Format(format, value);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
