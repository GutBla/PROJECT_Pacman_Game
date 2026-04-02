using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pacman_Game.Models
{
    public class Blinky : Ghost
    {
        public Blinky(double x, double y) : base(GhostColor.Red, x, y, 0)
        {
            SpawnPoint = ((int)x, (int)y);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
            => (pacman.X, pacman.Y);
    }
}