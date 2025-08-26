using ReactiveUI;

namespace Pacman_Game.Models
{
    public class Pacman : Character
    {
        public int Lives { get; set; } = 3;
        public int Score { get; set; }
        public double SpeedFactor { get; } = 1.0;
        public bool IsDying { get; set; } = false;
        public int DeathAnimationFrame { get; set; } = 0;

        public Pacman()
        {
            CurrentDirection = Direction.Right;
            NextDirection = Direction.Right;
        }

        public void Move(int[,] gameMap)
        {
            var (nextX, nextY) = CalculateNewPosition(NextDirection);
            int intNextX = (int)nextX;
            int intNextY = (int)nextY;

            if (IsValidMove(intNextX, intNextY, gameMap) && gameMap[intNextY, intNextX] == 0)
            {
                CurrentDirection = NextDirection;
            }

            var (newPosX, newPosY) = CalculateNewPosition(CurrentDirection);
            int intNewPosX = (int)newPosX;
            int intNewPosY = (int)newPosY;

            if (IsValidMove(intNewPosX, intNewPosY, gameMap))
            {
                X = newPosX;
                Y = newPosY;
            }

            if (X < 0) X = gameMap.GetLength(1) - 1;
            if (X >= gameMap.GetLength(1)) X = 0;
        }

        private (double, double) CalculateNewPosition(Direction direction) => direction switch
        {
            Direction.Up => (X, Y - SpeedFactor),
            Direction.Down => (X, Y + SpeedFactor),
            Direction.Left => (X - SpeedFactor, Y),
            Direction.Right => (X + SpeedFactor, Y),
            _ => (X, Y)
        };

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