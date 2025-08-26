using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Pacman_Game.Views
{
    public partial class GameOverWindow : Window
    {
        public GameOverWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void RestartButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var gameWindow = new GameWindow();
            gameWindow.Show();
            this.Close();
        }

        private void MenuButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            base.OnKeyDown(e);
        }
    }
}