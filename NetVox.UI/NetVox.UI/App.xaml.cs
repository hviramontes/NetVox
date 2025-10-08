using System;
using System.Windows;
using NetVox.UI.Views;

namespace NetVox.UI
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Start in explicit mode until we decide what to do.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 1) Show the chooser (no owner so we don't force MainWindow visibility)
            var chooser = new ModeChooserWindow();
            var result = chooser.ShowDialog();

            // If user cancels/closes, exit cleanly
            if (result != true || chooser.SelectedMode == null)
            {
                Shutdown();
                return;
            }

            // 2) Create the shell AFTER the user chooses,
            //    then set it as MainWindow to control app lifetime.
            var shell = new MainWindow();
            MainWindow = shell;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Advanced Mode: show the full shell
            if (chooser.SelectedMode == ModeChooserWindow.ModeChoice.Advanced)
            {
                shell.Show();
                return;
            }

            // Easy Mode: DO NOT show the shell first.
            // Launch straight into radio (your StartRadio() handles no-owner path).
            try
            {
                shell.LaunchEasyMode(
                    inputDeviceName: chooser.SelectedInputDevice,
                    outputDeviceName: chooser.SelectedOutputDevice
                );
                // Note: StartRadio() will show the Radio window and not set Owner since
                //       the shell hasn't been shown. No MainWindow flash.
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Easy Mode failed to launch:\n\n" + ex,
                    "NetVox",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                // Fallback: if Easy Mode fails, show the full shell so the user isn't stuck.
                shell.Show();
            }
        }
    }
}
