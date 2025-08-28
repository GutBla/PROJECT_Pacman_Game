namespace Pacman_Game.Models
{
    public abstract class GameItem
    {
        public int Points { get; protected set; }
        public required string Type { get; init; }
        public int X { get; set; }
        public int Y { get; set; }
    }


    public class Dot : GameItem
    {
        public Dot(int x, int y)
        {
            Points = 10;
            Type = "PD";
            X = x;
            Y = y;
        }
    }

    public class PowerPellet : GameItem
    {
        public PowerPellet(int x, int y)
        {
            Points = 50;
            Type = "PP";
            X = x;
            Y = y;
        }
    }

    public class Fruit : GameItem
    {
        public Fruit(string fruitType, int x, int y)
        {
            Type = fruitType;
            X = x;
            Y = y;

            Points = fruitType switch
            {
                "cherry" => 100,
                "strawberry" => 300,
                "orange" => 500,
                "apple" => 700,
                "melon" => 1000,
                "galaxian" => 2000,
                "bell" => 3000,
                "key" => 5000,
                _ => 100
            };
        }
    }
}
