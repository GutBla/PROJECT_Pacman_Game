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

        public double GetFrameSpeed()
        {
            if (IsInTunnel) return 0.4;
            if (IsPowerPelletActive) return 0.9;
            return 0.8;
        }

        public void Move(Map map, double deltaTime)
        {
            double frameSpeed = GetFrameSpeed();
            double speedPerSecond = frameSpeed * (1000.0 / Config.GameSpeed);
            double movement = speedPerSecond * deltaTime;

            var (nextX, nextY) = CalculateNewPosition(NextDirection, movement);

            // ✅ Manejo de túneles antes de validar colisión
            bool wrappingTunnel = false;
            if (nextX < 0) { nextX = map.Width - 1; wrappingTunnel = true; }
            else if (nextX >= map.Width) { nextX = 0; wrappingTunnel = true; }

            int intNextX = (int)Math.Round(nextX);
            int intNextY = (int)Math.Round(nextY);

            if (IsValidMove(intNextX, intNextY, map) || wrappingTunnel)
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

            // ✅ Snapping para evitar drift acumulativo
            X = Math.Round(X, 1);
            Y = Math.Round(Y, 1);
        }

        private new bool IsValidMove(int x, int y, Map map)
        {
            if (x < 0 || x >= map.Width) return true;   // Permitir wraparound
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