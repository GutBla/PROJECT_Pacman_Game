using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pacman_Game.Models
{
    public static class TileFlyweightFactory
    {
        private static Dictionary<string, TileFlyweight> _tiles = new();

        public static TileFlyweight GetTile(string textureKey, bool isBlocking)
        {
            string key = $"{textureKey}_{isBlocking}";
            if (!_tiles.ContainsKey(key))
            {
                _tiles[key] = new TileFlyweight(textureKey, isBlocking);
            }
            return _tiles[key];
        }

        public static TileFlyweight GetTileFromMapData(string textureKey, int gameMapValue)
        {
            bool isBlocking = gameMapValue == 1;
            return GetTile(textureKey, isBlocking);
        }
    }
}