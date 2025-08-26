using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Pacman_Game.Models;
using Pacman_Game.Services;
using System.Collections.Generic;
using System.Linq;

namespace Pacman_Game.Views
{
    public partial class HighScoresWindow : Window
    {
        public HighScoresWindow()
        {
            InitializeComponent();
            LoadScores();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void LoadScores()
        {
            var scores = ScoreService.LoadScores();

            var displayScores = new List<ScoreRecord>();
            for (int i = 1; i <= 10; i++)
            {
                var score = scores.FirstOrDefault(s => s.Rank == i);
                if (score != null)
                {
                    displayScores.Add(score);
                }
                else
                {
                    displayScores.Add(new ScoreRecord
                    {
                        Rank = i,
                        Name = "---",
                        Score = 0
                    });
                }
            }

            var scoresDataGrid = this.FindControl<DataGrid>("ScoresDataGrid");
            scoresDataGrid.ItemsSource = displayScores;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
