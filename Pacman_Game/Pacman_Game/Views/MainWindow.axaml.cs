using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Pacman_Game.Services;
using Pacman_Game.ViewModels;
using System;

namespace Pacman_Game.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            this.AttachDevTools();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            NavigationService.Instance.SetCurrentWindow(this);
        }
    }
}