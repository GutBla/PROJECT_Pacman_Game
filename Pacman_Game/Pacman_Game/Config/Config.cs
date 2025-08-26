using System.IO;
using System.Text.Json;

namespace Pacman_Game
{
    public static class Config
    {
        public static int InitialLives { get; set; } = 3;
        public static int GameSpeed { get; set; } = 100;

        private static readonly string ConfigFilePath = "config.json";

        static Config()
        {
            LoadConfig();
        }

        public static void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<ConfigData>(json);

                    InitialLives = config.InitialLives;
                    GameSpeed = config.GameSpeed;
                }
                catch
                {
                    SetDefaultValues();
                }
            }
            else
            {
                SetDefaultValues();
            }
        }

        public static void SaveConfig()
        {
            var configData = new ConfigData
            {
                InitialLives = InitialLives,
                GameSpeed = GameSpeed
            };

            var json = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }

        private static void SetDefaultValues()
        {
            InitialLives = 3;
            GameSpeed = 100;
        }
    }

    public class ConfigData
    {
        public int InitialLives { get; set; }
        public int GameSpeed { get; set; }
    }
}