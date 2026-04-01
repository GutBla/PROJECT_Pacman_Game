using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Pacman_Game.ViewModels;
using System;

namespace Pacman_Game.Views
{
    public partial class GameWindow : Window
    {
        public GameWindow()
        {
            InitializeComponent();
            this.AttachDevTools();
            DataContext = new GameViewModel();
            this.KeyDown += (_, e) => { if (e.Key == Key.Escape) this.Close(); };
            this.Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}