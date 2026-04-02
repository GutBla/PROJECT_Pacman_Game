using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Pacman_Game.Services;
using Pacman_Game.ViewModels;
using ReactiveUI;
using System;
using System.Reactive;

namespace Pacman_Game.Views
{
    public partial class VictoryWindow : Window
    {
        public VictoryWindow()
        {
            InitializeComponent();
            this.AttachDevTools();
        }

        public VictoryWindow(int score) : this()
        {
            DataContext = new VictoryViewModel(score);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (DataContext is VictoryViewModel vm)
            {
                vm.RequestClose += (_, _) => Close();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape && DataContext is VictoryViewModel vm)
            {
                vm.MenuCommand.Execute(Unit.Default);
            }
        }
    }
}