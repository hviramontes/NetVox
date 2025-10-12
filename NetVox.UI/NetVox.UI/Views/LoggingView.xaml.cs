using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace NetVox.UI.Views
{
    public partial class LoggingView : UserControl
    {
        public LoggingView()
        {
            InitializeComponent();

            BtnApply.Click += (_, __) => RaiseApply();
            BtnOpenFolder.Click += (_, __) => OpenFolderRequested?.Invoke();
            BtnOpenToday.Click += (_, __) => OpenCurrentLogRequested?.Invoke();
            BtnWriteTest.Click += (_, __) => WriteTestRequested?.Invoke();
        }

        public event Action<bool, int>? ApplyRequested;
        public event Action? OpenFolderRequested;
        public event Action? OpenCurrentLogRequested;
        public event Action? WriteTestRequested;

        public void SetState(bool verboseEnabled, int retentionDays)
        {
            ChkVerbose.IsChecked = verboseEnabled;
            TxtRetention.Text = retentionDays > 0 ? retentionDays.ToString(CultureInfo.InvariantCulture) : "10";
        }

        private void RaiseApply()
        {
            int days = 10;
            int.TryParse(TxtRetention.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out days);
            if (days <= 0) days = 10;

            bool verbose = ChkVerbose.IsChecked == true;
            ApplyRequested?.Invoke(verbose, days);
        }

        public void SetHint(string text)
        {
            LblHint.Text = text ?? "";
        }
    }
}
