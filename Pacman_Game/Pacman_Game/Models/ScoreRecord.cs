using System.Text.Json.Serialization;

namespace Pacman_Game.Models
{
    public class ScoreRecord
    {
        public int Rank { get; set; }
        public int Score { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}