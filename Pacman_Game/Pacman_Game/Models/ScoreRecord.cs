using System.Text.Json.Serialization;

namespace Pacman_Game.Models
{
    public class ScoreRecord
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}