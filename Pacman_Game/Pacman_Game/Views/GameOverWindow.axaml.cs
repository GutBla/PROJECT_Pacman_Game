using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Pacman_Game.Services;
using Pacman_Game.Views;

namespace Pacman_Game.Views
{
    public partial class GameOverWindow : Window
    {
        public GameOverWindow()
        {
            InitializeComponent();
            this.AttachDevTools();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void RestartButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigationService.Instance.NavigateTo<GameWindow>();
            Close();
        }

        private void MenuButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigationService.Instance.NavigateToMainMenu();
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                NavigationService.Instance.NavigateToMainMenu();
                Close();
            }
        }
    }
}