using System;
using System.Linq;

namespace Pacman_Game.Models
{
    public class GhostFactory
    {
        private Blinky blinky;

        public Ghost CreateGhost(GhostColor color, double x, double y)
        {
            switch (color)
            {
                case GhostColor.Red:
                    blinky = new Blinky(x, y);
                    return blinky;
                case GhostColor.Pink:
                    return new Pinky(x, y);
                case GhostColor.Blue:
                    if (blinky == null)
                        throw new InvalidOperationException("Blinky debe ser creado primero");
                    return new Inky(x, y, blinky);
                case GhostColor.Orange:
                    return new Clyde(x, y);
                default:
                    throw new ArgumentException("Color de fantasma no válido");
            }
        }
    }
}