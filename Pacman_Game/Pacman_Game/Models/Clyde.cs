using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pacman_Game.Models
{
    public class Clyde : Ghost
    {
        public Clyde(double x, double y) : base(GhostColor.Orange, x, y, 60)
        {
            SpawnPoint = ((int)x, (int)y);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            double distance = Math.Sqrt(Math.Pow(X - pacman.X, 2) + Math.Pow(Y - pacman.Y, 2));
            if (distance < 8)
            {
                var corner = GetScatterCorner(null);
                return corner;
            }
            return (pacman.X, pacman.Y);
        }
    }
}