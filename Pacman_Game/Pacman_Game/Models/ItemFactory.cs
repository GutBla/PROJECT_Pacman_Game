namespace Pacman_Game.Models
{
    public interface IItemFactory
    {
        GameItem? CreateItem(string itemType, int x, int y);
    }

    public class ItemFactory : IItemFactory
    {
        public GameItem? CreateItem(string type, int x, int y)
        {
            switch (type)
            {
                case "PD":
                    return new Dot(x, y) { Type = "PD" };
                case "PP":
                    return new PowerPellet(x, y) { Type = "PP" };
                case "cherry":
                case "strawberry":
                case "orange":
                case "apple":
                case "melon":
                case "galaxian":
                case "bell":
                case "key":
                    return new Fruit(type, x, y) { Type = type };
                default:
                    return null;
            }
        }
    }
}
