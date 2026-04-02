using ReactiveUI;
using System.Reactive;
using Pacman_Game.Services;
using Pacman_Game.Views;
using Pacman_Game.Managers;

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
            SoundManager.Instance.PlaySound("game_credit_sound");
            NavigationService.Instance.NavigateTo<GameWindow>();
        }

        private async void HowToPlay()
        {
            await NavigationService.Instance.ShowModal<HowToPlayWindow>();
        }

        private async void HighScores()
        {
            await NavigationService.Instance.ShowModal<HighScoresWindow>();
        }

        private async void Settings()
        {
            await NavigationService.Instance.ShowModal<SettingsWindow>();
        }
    }
}