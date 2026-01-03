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
                // Gets version from csproj - auto-increments with each build based on git commit count
                var version = AppInfo.Current.VersionString;
                VersionLabel.Text = $"v{version}";
            }
            catch
            {
                // Fallback version if AppInfo is not available
                VersionLabel.Text = "v1.0.0";
            }
        }
    }
}
