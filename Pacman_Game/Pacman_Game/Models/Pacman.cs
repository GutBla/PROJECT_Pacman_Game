using ReactiveUI;
using System;

namespace Pacman_Game.Models
{
    public class Pacman : Character
    {
        public int Lives { get; set; }
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

        // Velocidad por frame original (se usará para calcular velocidad por segundo)
        public double GetFrameSpeed()
        {
            if (IsInTunnel) return 0.4;
            if (IsPowerPelletActive) return 0.9;
            return 0.8;
        }

        public void Move(Map map, double deltaTime)
        {
            IsInTunnel = (Y >= 13 && Y <= 14) && (X < 1 || X > map.Width - 2);

            // Calcular velocidad por segundo: frameSpeed * (1000 / GameSpeed)
            double frameSpeed = GetFrameSpeed();
            double speedPerSecond = frameSpeed * (1000.0 / Config.GameSpeed);
            double movement = speedPerSecond * deltaTime;

            var (nextX, nextY) = CalculateNewPosition(NextDirection, movement);
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
                var (newPosX, newPosY) = CalculateNewPosition(CurrentDirection, movement);
                int intNewPosX = (int)Math.Round(newPosX);
                int intNewPosY = (int)Math.Round(newPosY);

                if (IsValidMove(intNewPosX, intNewPosY, map))
                {
                    X = newPosX;
                    Y = newPosY;
                }
            }

            // Teletransporte en túneles
            if (X < 0) X = map.Width - 1;
            if (X >= map.Width) X = 0;
        }

        private new bool IsValidMove(int x, int y, Map map)
        {
            if (x < 0) return true;
            if (x >= map.Width) return true;
            return y >= 0 && y < map.Height && !map.IsBlocking(x, y);
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