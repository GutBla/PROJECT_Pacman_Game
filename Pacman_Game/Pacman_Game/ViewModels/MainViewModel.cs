using ReactiveUI;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Pacman_Game.Views;

namespace Pacman_Game.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> StartGameCommand { get; }
        public ReactiveCommand<Unit, Unit> HowToPlayCommand { get; }
        public ReactiveCommand<Unit, Unit> HighScoresCommand { get; }
        public ReactiveCommand<Unit, Unit> SettingsCommand { get; }

        public MainViewModel()
        {
            StartGameCommand = ReactiveCommand.Create(StartGame);
            HowToPlayCommand = ReactiveCommand.Create(HowToPlay);
            HighScoresCommand = ReactiveCommand.Create(HighScores);
            SettingsCommand = ReactiveCommand.Create(Settings);
        }

        private void StartGame()
        {
            var gameWindow = new GameWindow();
            gameWindow.Show();
            CloseCurrentWindow();
        }

        private void CloseCurrentWindow()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Close();
            }
        }

        private void HowToPlay()
        {
            
        }

        private void HighScores()
        {
            
        }

        private void Settings()
        {
            
        }
    }
}