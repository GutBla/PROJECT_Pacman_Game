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
}