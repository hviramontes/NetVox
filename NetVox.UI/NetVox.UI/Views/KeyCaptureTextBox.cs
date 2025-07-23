using System.Windows.Controls;
using System.Windows.Input;

namespace NetVox.UI.Views
{
    public class KeyCaptureTextBox : TextBox
    {
        public KeyCaptureTextBox()
        {
            this.IsReadOnly = true;
            this.Focusable = true;
            this.PreviewKeyDown += KeyCaptureTextBox_PreviewKeyDown;
        }

        private void KeyCaptureTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab || e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                return; // ignore modifier-only presses
            }

            this.Text = e.Key.ToString();
            e.Handled = true;
        }
    }
}
