using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pacman_Game.Models
{
    public class Inky : Ghost
    {
        private readonly Blinky _blinky;

        public Inky(double x, double y, Blinky blinky) : base(GhostColor.Blue, x, y, 30)
        {
            _blinky = blinky;
            SpawnPoint = ((int)x, (int)y);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            var (blX, blY) = (_blinky.X, _blinky.Y);
            var (pX, pY) = pacman.CurrentDirection switch
            {
                Direction.Left => (pacman.X - 2, pacman.Y),
                Direction.Right => (pacman.X + 2, pacman.Y),
                Direction.Up => (pacman.X, pacman.Y - 2),
                Direction.Down => (pacman.X, pacman.Y + 2),
                _ => (pacman.X, pacman.Y)
            };
            double tX = blX + 2 * (pX - blX);
            double tY = blY + 2 * (pY - blY);
            double d = Math.Sqrt(Math.Pow(tX - pacman.X, 2) + Math.Pow(tY - pacman.Y, 2));
            if (d > 16)
            {
                double r = 16 / d;
                tX = pacman.X + (tX - pacman.X) * r;
                tY = pacman.Y + (tY - pacman.Y) * r;
            }
            return (tX, tY);
        }
    }
}