using Pacman_Game.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Pacman_Game.Services
{
    public static class ScoreService
    {
        private static readonly string ScoresDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "scores");
        private static readonly string ScoresFile = Path.Combine(ScoresDirectory, "List_Scores.json");

        public static List<ScoreRecord> LoadScores()
        {
            var scores = new List<ScoreRecord>();
            try
            {
                if (!File.Exists(ScoresFile))
                {
                    Directory.CreateDirectory(ScoresDirectory);
                    return scores;
                }

                string json = File.ReadAllText(ScoresFile);
                scores = JsonSerializer.Deserialize<List<ScoreRecord>>(json) ?? new List<ScoreRecord>();

                return scores.OrderBy(s => s.Rank).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading scores: {ex.Message}");
                return scores;
            }
        }

        public static bool SaveScore(ScoreRecord newScore)
        {
            try
            {
                Directory.CreateDirectory(ScoresDirectory);
                var scores = LoadScores();

                scores.Add(newScore);
                var topScores = scores
                    .OrderByDescending(s => s.Score)
                    .ThenBy(s => s.Name)
                    .Take(10)
                    .ToList();

                for (int i = 0; i < topScores.Count; i++)
                {
                    topScores[i].Rank = i + 1;
                }

                string json = JsonSerializer.Serialize(topScores,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(ScoresFile, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving score: {ex.Message}");
                return false;
            }
        }
    }
}