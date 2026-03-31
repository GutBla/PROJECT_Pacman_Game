using System;
using System.IO;
using System.Text.Json;

namespace Pacman_Game
{
    public static class Config
    {
        public static int InitialLives { get; set; } = 3;
        public static int GameSpeed { get; set; } = 100;

        private const string ConfigFilePath = "config.json";
        private const string LogFilePath = "game_errors.log";
        private const int MinLives = 1;
        private const int MaxLives = 50;
        private const int MinSpeed = 50;
        private const int MaxSpeed = 500;

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        static Config() => LoadConfig();

        public static void LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                SetDefaultValues();
                SaveConfig();
                return;
            }

            try
            {
                var json = File.ReadAllText(ConfigFilePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    LogWarning("Config file is empty, resetting to defaults.");
                    SetDefaultValues();
                    SaveConfig();
                    return;
                }

                var config = JsonSerializer.Deserialize<ConfigData>(json);

                if (config == null)
                {
                    LogWarning("Config deserialization returned null, resetting to defaults.");
                    SetDefaultValues();
                    SaveConfig();
                    return;
                }

                if (!IsValidConfig(config))
                {
                    LogWarning($"Invalid config values (Lives={config.InitialLives}, Speed={config.GameSpeed}), resetting.");
                    SetDefaultValues();
                    SaveConfig();
                    return;
                }

                InitialLives = config.InitialLives;
                GameSpeed = config.GameSpeed;
            }
            catch (JsonException ex)
            {
                LogError($"JSON parsing error: {ex.Message}");
                SetDefaultValues();
                SaveConfig();
            }
            catch (IOException ex)
            {
                LogError($"File I/O error: {ex.Message}");
                SetDefaultValues();
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"Access denied: {ex.Message}");
                SetDefaultValues();
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error: {ex.GetType().Name} - {ex.Message}");
                SetDefaultValues();
            }
        }

        public static void SaveConfig()
        {
            try
            {
                var configData = new ConfigData
                {
                    InitialLives = InitialLives,
                    GameSpeed = GameSpeed
                };

                if (!IsValidConfig(configData))
                {
                    LogWarning("Attempted to save invalid config, using defaults.");
                    SetDefaultValues();
                    configData.InitialLives = InitialLives;
                    configData.GameSpeed = GameSpeed;
                }

                if (File.Exists(ConfigFilePath))
                    File.Copy(ConfigFilePath, $"{ConfigFilePath}.backup", overwrite: true);

                File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(configData, JsonOptions));
            }
            catch (IOException ex) { LogError($"Failed to save config: {ex.Message}"); }
            catch (Exception ex) { LogError($"Unexpected save error: {ex.GetType().Name} - {ex.Message}"); }
        }

        private static void SetDefaultValues()
        {
            InitialLives = 3;
            GameSpeed = 100;
        }

        private static bool IsValidConfig(ConfigData config) =>
            config.InitialLives >= MinLives && config.InitialLives <= MaxLives &&
            config.GameSpeed >= MinSpeed && config.GameSpeed <= MaxSpeed;

        private static void LogError(string message) => Log("ERROR", message);
        private static void LogWarning(string message) => Log("WARN", message);

        private static void Log(string level, string message)
        {
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
            Console.WriteLine(entry.TrimEnd());
            try { File.AppendAllText(LogFilePath, entry); }
            catch { /* ignore log failures */ }
        }
    }

    public sealed class ConfigData
    {
        public int InitialLives { get; set; }
        public int GameSpeed { get; set; }
    }
}