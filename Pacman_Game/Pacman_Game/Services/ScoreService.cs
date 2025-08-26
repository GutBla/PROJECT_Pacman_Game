using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Pacman_Game.Models;

namespace Pacman_Game.Services
{
    public static class ScoreService
    {
        private static readonly string AppDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PacmanGame");
        private static readonly string ScoresDirectory = Path.Combine(AppDataPath, "scores");
        private static readonly string ScoresFile = Path.Combine(ScoresDirectory, "List_Scores.json");

        public static List<ScoreRecord> LoadScores()
        {
            try
            {
                if (!File.Exists(ScoresFile))
                    return new List<ScoreRecord>();

                var json = File.ReadAllText(ScoresFile);
                return JsonSerializer.Deserialize<List<ScoreRecord>>(json) ?? new List<ScoreRecord>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading scores: {ex.Message}");
                return new List<ScoreRecord>();
            }
        }

        public static bool SaveScore(ScoreRecord newScore)
        {
            try
            {
                Console.WriteLine("=== INICIANDO GUARDADO DE PUNTUACIÓN ===");
                Console.WriteLine($"Ruta de ApplicationData: {Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}");
                Console.WriteLine($"Ruta completa de scores: {ScoresFile}");

                // Verificar y crear directorio
                Console.WriteLine($"Creando directorio: {ScoresDirectory}");
                Directory.CreateDirectory(ScoresDirectory);

                // Verificar si el directorio fue creado
                bool directoryExists = Directory.Exists(ScoresDirectory);
                Console.WriteLine($"¿Directorio existe después de crearlo? {directoryExists}");

                var scores = LoadScores();
                Console.WriteLine($"Scores cargados: {scores.Count}");

                scores.Add(newScore);
                scores.Sort((a, b) => b.Score.CompareTo(a.Score));

                for (int i = 0; i < scores.Count; i++)
                {
                    scores[i].Rank = i + 1;
                }

                if (scores.Count > 10)
                {
                    scores = scores.GetRange(0, 10);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(scores, options);
                Console.WriteLine($"JSON a guardar: {json}");

                File.WriteAllText(ScoresFile, json);

                // Verificar si el archivo fue creado
                bool fileExists = File.Exists(ScoresFile);
                Console.WriteLine($"¿Archivo existe después de guardar? {fileExists}");

                if (fileExists)
                {
                    Console.WriteLine($"Tamaño del archivo: {new FileInfo(ScoresFile).Length} bytes");
                }

                Console.WriteLine("=== PUNTUACIÓN GUARDADA EXITOSAMENTE ===");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ERROR AL GUARDAR ===");
                Console.WriteLine($"Mensaje: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }
    }
}