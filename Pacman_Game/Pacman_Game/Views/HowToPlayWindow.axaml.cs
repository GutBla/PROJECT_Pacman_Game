using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Pacman_Game.Views
{
    public partial class HowToPlayWindow : Window
    {
        public HowToPlayWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void BackButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
            => this.Close();
    }
}