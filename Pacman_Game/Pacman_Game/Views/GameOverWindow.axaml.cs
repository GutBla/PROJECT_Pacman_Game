using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;

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

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void RestartButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            new GameWindow().Show();
            this.Close();
        }

        private void MenuButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            new MainWindow().Show();
            this.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) { new MainWindow().Show(); this.Close(); }
        }

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as IDisposable)?.Dispose();
            base.OnClosed(e);
        }
    }
}