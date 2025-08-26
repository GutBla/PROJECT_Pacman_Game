namespace Pacman_Game.Models
{
    public class Map
    {
        public TileFlyweight[,] Tiles { get; private set; }
        public string[,] Elements { get; private set; } 
        public int Width { get; private set; }
        public int Height { get; private set; } 

        public Map(int width, int height)
        {
            Width = width;
            Height = height;
            Tiles = new TileFlyweight[height, width];
            Elements = new string[height, width];
        }


        public void InitializeFromData(int[,] gameMap, string[,] mapTextures, string[,] elements)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    string textureKey = mapTextures[y, x];
                    Tiles[y, x] = TileFlyweightFactory.GetTileFromMapData(textureKey, gameMap[y, x]);
                    Elements[y, x] = elements[y, x];
                }
            }
        }

        // Indica si una celda es bloqueante (pared)
        public bool IsBlocking(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return true;

            return Tiles[y, x].IsBlocking;
        }

        // Devuelve la clave de textura de un tile
        public string GetTextureKey(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return "";

            return Tiles[y, x].TextureKey;
        }
    }
}
