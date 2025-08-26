using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Pacman_Game.Views
{
    public partial class MessageDialog : Window
    {
        public MessageDialog(string message)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            var messageTextBlock = this.FindControl<TextBlock>("MessageText");
            if (messageTextBlock != null)
            {
                messageTextBlock.Text = message;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}