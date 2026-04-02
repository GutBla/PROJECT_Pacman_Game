using Pacman_Game.Models;
using Pacman_Game.Services;
using Pacman_Game.Views;
using ReactiveUI;
using System;
using System.Reactive;

namespace Pacman_Game.ViewModels
{
    public sealed class VictoryViewModel : ViewModelBase
    {
        private string _playerName = string.Empty;
        private int _score;
        private string _statusMessage = string.Empty;
        public event EventHandler? RequestClose;

        public string PlayerName
        {
            get => _playerName;
            set => this.RaiseAndSetIfChanged(ref _playerName, value);
        }

        public int Score
        {
            get => _score;
            private set => this.RaiseAndSetIfChanged(ref _score, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public ReactiveCommand<Unit, Unit> RestartCommand { get; }
        public ReactiveCommand<Unit, Unit> MenuCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveScoreCommand { get; }

        public VictoryViewModel(int score)
        {
            Score = score;
            RestartCommand = ReactiveCommand.Create(RestartGame);
            MenuCommand = ReactiveCommand.Create(ReturnToMenu);
            var canSave = this.WhenAnyValue(x => x.PlayerName, name => !string.IsNullOrWhiteSpace(name));
            SaveScoreCommand = ReactiveCommand.Create(SaveScore, canSave);
        }

        private void RestartGame()
        {
            NavigationService.Instance.NavigateTo<GameWindow>();
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void ReturnToMenu()
        {
            NavigationService.Instance.NavigateToMainMenu();
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void SaveScore()
        {
            try
            {
                var record = new ScoreRecord
                {
                    Score = Score,
                    Name = PlayerName.Trim(),
                    Rank = 0
                };
                StatusMessage = ScoreService.SaveScore(record)
                    ? "Puntuación guardada exitosamente."
                    : "Error al guardar la puntuación.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}