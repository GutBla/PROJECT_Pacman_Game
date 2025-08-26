using Pacman_Game.Models;
using Pacman_Game.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Pacman_Game.ViewModels
{
    public class HighScoresViewModel : ViewModelBase
    {
        private ObservableCollection<ScoreRecord> _scores;
        public ObservableCollection<ScoreRecord> Scores
        {
            get => _scores;
            set => this.RaiseAndSetIfChanged(ref _scores, value);
        }

        public HighScoresViewModel()
        {
            LoadScores();
        }

        private void LoadScores()
        {
            // Genera la lista de las 10 posiciones
            var scores = ScoreService.LoadScores();
            var displayScores = new List<ScoreRecord>();
            for (int i = 1; i <= 10; i++)
            {
                var score = scores.FirstOrDefault(s => s.Rank == i);
                displayScores.Add(score ?? new ScoreRecord
                {
                    Rank = i,
                    Name = "---",
                    Score = 0
                });
            }

            Scores = new ObservableCollection<ScoreRecord>(displayScores);
        }
    }
}