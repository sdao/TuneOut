using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace TuneOut
{
    public sealed partial class SettingsLegal : UserControl
    {
        public SettingsLegal()
        {
            this.InitializeComponent();
            this.Loaded += (sender, e) =>
            {
                SettingsUIManager.SettingsCharmOpen = true;
            };
            this.Unloaded += (sender, e) =>
            {
                SettingsUIManager.SettingsCharmOpen = false;
            };
        }
    }
}
