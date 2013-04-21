// http://weblogs.asp.net/broux/archive/2012/07/03/windows-8-an-application-bar-toggle-button.aspx
// (c) 2012 Benjamin Roux.
// Simplified a bit.

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TuneOut
{
    public class AppBarToggleButton : Button
    {
        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }
        
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(AppBarToggleButton), new PropertyMetadata(false, (o, e) => (o as AppBarToggleButton).IsCheckedChanged()));
        
        public Style CheckedStyle
        {
            get { return (Style)GetValue(CheckedStyleProperty); }
            set { SetValue(CheckedStyleProperty, value); }
        }
        
        public static readonly DependencyProperty CheckedStyleProperty =
            DependencyProperty.Register("CheckedStyle", typeof(Style), typeof(AppBarToggleButton), null);

        public string AutomationName
        {
            get { return (string)GetValue(Windows.UI.Xaml.Automation.AutomationProperties.NameProperty); }
            set { SetValue(Windows.UI.Xaml.Automation.AutomationProperties.NameProperty, value); }
        }

        public static readonly DependencyProperty AutomationNameProperty =
           DependencyProperty.Register("AutomationName", typeof(string), typeof(AppBarToggleButton), null);

        public string CheckedAutomationName
        {
            get { return (string)GetValue(CheckedAutomationNameProperty); }
            set { SetValue(CheckedAutomationNameProperty, value); }
        }

        public static readonly DependencyProperty CheckedAutomationNameProperty =
            DependencyProperty.Register("CheckedAutomationName", typeof(string), typeof(AppBarToggleButton), null);

        public bool AutoToggle
        {
            get { return (bool)GetValue(AutoToggleProperty); }
            set { SetValue(AutoToggleProperty, value); }
        }
        
        public static readonly DependencyProperty AutoToggleProperty =
            DependencyProperty.Register("AutoToggle", typeof(bool), typeof(AppBarToggleButton), null);
        
        private Style _style;
        private string _automationName;
        
        private void IsCheckedChanged()
        {
            if (IsChecked)
            {
                if (CheckedStyle != null)
                {
                    _style = Style;
                    Style = CheckedStyle;
                }

                if (CheckedAutomationName != null)
                {
                    _automationName = AutomationName;
                    AutomationName = CheckedAutomationName;
                }
            }
            else
            {
                if (CheckedStyle != null)
                {
                    Style = _style;
                }

                if (CheckedAutomationName != null)
                {
                    AutomationName = _automationName;
                }
            }
        }
        
        protected override void OnTapped(Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            base.OnTapped(e);
            if (AutoToggle) IsChecked = !IsChecked;
        }
    }
}