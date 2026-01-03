using System.Reflection;

namespace PrinterAPP
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            SetVersionLabel();
        }

        private void SetVersionLabel()
        {
            try
            {
                var version = AppInfo.Current.VersionString;
                VersionLabel.Text = $"Version {version}";
            }
            catch
            {
                VersionLabel.Text = "Version 1.0.0";
            }
        }
    }
}
