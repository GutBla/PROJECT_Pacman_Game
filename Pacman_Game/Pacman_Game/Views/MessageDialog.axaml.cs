using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Pacman_Game.Views
{
    public partial class MessageDialog : Window
    {
        public MessageDialog()
        {
            InitializeComponent();
            this.AttachDevTools();
        }

        public MessageDialog(string message) : this()
        {
            var tb = this.FindControl<TextBlock>("MessageText");
            if (tb != null) tb.Text = message;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        private void OKButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}