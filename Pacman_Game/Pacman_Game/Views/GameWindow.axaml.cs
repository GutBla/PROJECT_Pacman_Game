using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Pacman_Game.Services;
using Pacman_Game.ViewModels;
using System;

namespace Pacman_Game.Views
{
    public partial class GameWindow : Window
    {
        private GameViewModel? _viewModel;

        public GameWindow()
        {
            InitializeComponent();
            this.AttachDevTools();
            DataContext = _viewModel = new GameViewModel();
            this.KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            this.Closed += OnClosed;

            if (_viewModel != null)
            {
                _viewModel.GameOverRequested += OnGameOverRequested;
                _viewModel.VictoryRequested += OnVictoryRequested;
                _viewModel.CriticalErrorRequested += OnCriticalErrorRequested;
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnGameOverRequested(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await NavigationService.Instance.ShowModal<GameOverWindow>();
                Close();
            });
        }

        private void OnVictoryRequested(object? sender, int score)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await NavigationService.Instance.ShowModal<VictoryWindow>(score);
                Close();
            });
        }

        private void OnCriticalErrorRequested(object? sender, Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await NavigationService.Instance.ShowModal<MessageDialog>(ex.Message);
                Close();
            });
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.GameOverRequested -= OnGameOverRequested;
                _viewModel.VictoryRequested -= OnVictoryRequested;
                _viewModel.CriticalErrorRequested -= OnCriticalErrorRequested;
                _viewModel.Dispose();
            }
            (DataContext as IDisposable)?.Dispose();
        }
    }
}