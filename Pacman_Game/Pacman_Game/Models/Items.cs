namespace Pacman_Game.Models
{
    public abstract class GameItem
    {
        public int Points { get; protected set; }
        public required string Type { get; init; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    // dot
    public class Dot : GameItem
    {
        public Dot(int x, int y)
        {
            Points = 10;  // Valor de puntos
            Type = "PD";  // Identificador de tipo
            X = x;
            Y = y;
        }
    }

    // Power Pellets
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

    // frutas
    public class Fruit : GameItem
    {
        public Fruit(string fruitType, int x, int y)
        {
            Type = fruitType;
            X = x;
            Y = y;
            Points = 100;
        }
    }
}
