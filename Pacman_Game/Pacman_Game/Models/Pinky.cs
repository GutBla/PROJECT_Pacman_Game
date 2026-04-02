using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pacman_Game.Models
{
    public class Pinky : Ghost
    {
        public Pinky(double x, double y) : base(GhostColor.Pink, x, y, 7)
        {
            SpawnPoint = ((int)x, (int)y);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            const int offset = 4;
            return pacman.CurrentDirection switch
            {
                Direction.Left => (pacman.X - offset, pacman.Y),
                Direction.Right => (pacman.X + offset, pacman.Y),
                Direction.Up => (pacman.X, pacman.Y - offset),
                Direction.Down => (pacman.X, pacman.Y + offset),
                _ => (pacman.X, pacman.Y)
            };
        }
    }
}