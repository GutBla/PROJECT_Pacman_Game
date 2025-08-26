using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Pacman_Game.ViewModels;

namespace Pacman_Game.Views
{
    public partial class GameWindow : Window
    {
        public GameWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            DataContext = new GameViewModel();

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    this.Close();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}