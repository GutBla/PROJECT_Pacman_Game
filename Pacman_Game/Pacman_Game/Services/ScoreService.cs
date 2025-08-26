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
        private static readonly string AppDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PacmanGame");
        private static readonly string ScoresDirectory = Path.Combine(AppDataPath, "scores");
        private static readonly string ScoresFile = Path.Combine(ScoresDirectory, "scores.txt");

        public static List<ScoreRecord> LoadScores()
        {
            var scores = new List<ScoreRecord>();

            try
            {
                if (!File.Exists(ScoresFile))
                    return scores;

                using (StreamReader reader = new StreamReader(ScoresFile))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var parts = line.Split('|');
                        if (parts.Length == 3)
                        {
                            scores.Add(new ScoreRecord
                            {
                                Rank = int.Parse(parts[0]),
                                Score = int.Parse(parts[1]),
                                Name = parts[2]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading scores: {ex.Message}");
            }

            return scores.OrderBy(s => s.Rank).ToList();
        }

        public static bool SaveScore(ScoreRecord newScore)
        {
            try
            {
                Directory.CreateDirectory(ScoresDirectory);

                var scores = LoadScores();
                scores.Add(newScore);

                // Keep only top 10 scores
                var topScores = scores.OrderByDescending(s => s.Score)
                                     .Take(10)
                                     .ToList();

                using (StreamWriter writer = new StreamWriter(ScoresFile, false))
                {
                    for (int i = 0; i < topScores.Count; i++)
                    {
                        topScores[i].Rank = i + 1;
                        writer.WriteLine($"{topScores[i].Rank}|{topScores[i].Score}|{topScores[i].Name}");
                    }
                }

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