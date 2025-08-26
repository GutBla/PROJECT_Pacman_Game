using ReactiveUI;
using System;

namespace Pacman_Game.Models
{
    public class Pacman : Character
    {
        public int Lives { get; set; } = 3;
        public int Score { get; set; }
        public bool IsDying { get; set; } = false;
        public int DeathAnimationFrame { get; set; } = 0;
        public bool IsInTunnel { get; set; } = false;
        public bool IsPowerPelletActive { get; set; } = false;

        public Pacman()
        {
            CurrentDirection = Direction.Right;
            NextDirection = Direction.Right;
        }

        public double GetCurrentSpeed()
        {
            if (IsInTunnel) return 0.4;
            if (IsPowerPelletActive) return 0.9;
            return 0.8;
        }

        public void Move(Map map)
        {
            IsInTunnel = (Y >= 13 && Y <= 14) && (X < 1 || X > map.Width - 2);
            var (nextX, nextY) = CalculateNewPosition(NextDirection);
            int intNextX = (int)Math.Round(nextX);
            int intNextY = (int)Math.Round(nextY);

            if (IsValidMove(intNextX, intNextY, map))
            {
                CurrentDirection = NextDirection;
                X = nextX;
                Y = nextY;
            }
            else
            {
                var (newPosX, newPosY) = CalculateNewPosition(CurrentDirection);
                int intNewPosX = (int)Math.Round(newPosX);
                int intNewPosY = (int)Math.Round(newPosY);

                if (IsValidMove(intNewPosX, intNewPosY, map))
                {
                    X = newPosX;
                    Y = newPosY;
                }
            }
            if (X < 0) X = map.Width - 1;
            if (X >= map.Width) X = 0;
        }
        private new bool IsValidMove(int x, int y, Map map)
        {
            if (x < 0) return true;
            if (x >= map.Width) return true;

            return y >= 0 && y < map.Height && !map.IsBlocking(x, y);
        }

        private (double, double) CalculateNewPosition(Direction direction)
        {
            double speed = GetCurrentSpeed();
            return direction switch
            {
                Direction.Up => (X, Y - speed),
                Direction.Down => (X, Y + speed),
                Direction.Left => (X - speed, Y),
                Direction.Right => (X + speed, Y),
                _ => (X, Y)
            };
        }

        private bool IsValidMove(int x, int y, int[,] map)
        {
            if (x < 0) x = map.GetLength(1) - 1;
            if (x >= map.GetLength(1)) x = 0;
            return y >= 0 && y < map.GetLength(0) && map[y, x] == 0;
        }

        public void ResetPosition()
        {
            X = 13;
            Y = 23;
            CurrentDirection = Direction.Right;
            NextDirection = Direction.Right;
        }
    }
}