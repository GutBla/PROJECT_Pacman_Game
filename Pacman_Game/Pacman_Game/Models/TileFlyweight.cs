using System.Collections.Generic;

namespace Pacman_Game.Models
{
    public class TileFlyweight
    {
        public string TextureKey { get; } 
        public bool IsBlocking { get; }

        public TileFlyweight(string textureKey, bool isBlocking)
        {
            TextureKey = textureKey;
            IsBlocking = isBlocking;
        }
    }

    public static class TileFlyweightFactory
    {
        private static Dictionary<string, TileFlyweight> _tiles = new();

        public static TileFlyweight GetTile(string textureKey, bool isBlocking) // Retorna tile único
        {
            string key = $"{textureKey}_{isBlocking}";
            if (!_tiles.ContainsKey(key))
            {
                _tiles[key] = new TileFlyweight(textureKey, isBlocking);
            }
            return _tiles[key];
        }

        public static TileFlyweight GetTileFromMapData(string textureKey, int gameMapValue) // Determina si bloquea según valor del mapa
        {
            bool isBlocking = gameMapValue == 1;
            return GetTile(textureKey, isBlocking);
        }
    }
}
