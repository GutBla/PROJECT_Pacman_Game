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
            var scores = ScoreService.LoadScores();

            Console.WriteLine($"Scores loaded: {scores?.Count}");
            foreach (var score in scores)
            {
                Console.WriteLine($"Rank: {score.Rank}, Name: {score.Name}, Score: {score.Score}");
            }

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