using System;
using System.Collections.Generic;
using Avalonia.Threading;
using ReactiveUI;

namespace Pacman_Game.Models
{
    public enum GhostColor { Red, Pink, Blue, Orange }
    public enum GhostState { Chase, Scatter, Frightened, Eaten }

    public class Ghost : Character
    {
        public GhostColor Color { get; }
        public GhostState State { get; set; } = GhostState.Scatter;
        public double SpeedFactor { get; } = 0.85;

        private Random random = new Random();
        private DispatcherTimer stateTimer;
        private DispatcherTimer frightenedTimer;

        public (int X, int Y) SpawnPoint => (14, 14);

        public Ghost(GhostColor color)
        {
            Color = color;
            Speed = 1;
            X = SpawnPoint.X;
            Y = SpawnPoint.Y;
            CurrentDirection = Direction.Left;
            SetupStateTimers();
        }

        private void SetupStateTimers()
        {
            stateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(7)
            };
            stateTimer.Tick += (s, e) => CycleStates();
            stateTimer.Start();

            frightenedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            frightenedTimer.Tick += (s, e) =>
            {
                if (State == GhostState.Frightened)
                    State = GhostState.Chase;
            };
        }

        private void CycleStates()
        {
            if (State == GhostState.Scatter)
            {
                State = GhostState.Chase;
            }
            else if (State == GhostState.Chase)
            {
                State = GhostState.Scatter;
            }
        }

        public void ChasePacman(Pacman pacman, int[,] gameMap)
        {
            switch (State)
            {
                case GhostState.Chase:
                    var target = GetTargetPosition(pacman);
                    MoveTowardsTarget(target.X, target.Y, gameMap);
                    break;
                case GhostState.Scatter:
                    var corner = GetScatterCorner(gameMap);
                    MoveTowardsTarget(corner.X, corner.Y, gameMap);
                    break;
                case GhostState.Frightened:
                    MoveRandomly(gameMap);
                    break;
                case GhostState.Eaten:
                    MoveTowardsTarget(SpawnPoint.X, SpawnPoint.Y, gameMap);
                    if (Math.Abs(X - SpawnPoint.X) < 0.1 && Math.Abs(Y - SpawnPoint.Y) < 0.1)
                    {
                        State = GhostState.Scatter;
                    }
                    break;
            }
        }

        private (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            return Color switch
            {
                GhostColor.Red => (pacman.X, pacman.Y),
                GhostColor.Pink => (pacman.X + 4 * GetDirectionMultiplier(pacman.CurrentDirection), pacman.Y),
                GhostColor.Blue => CalculateBlueTarget(pacman),
                _ => (pacman.X, pacman.Y)
            };
        }

        private (double X, double Y) CalculateBlueTarget(Pacman pacman)
        {
            var redGhostPos = (X: 0.0, Y: 0.0);
            var vector = (pacman.X - redGhostPos.X, pacman.Y - redGhostPos.Y);
            return (pacman.X + vector.Item1 * 2, pacman.Y + vector.Item2 * 2);
        }

        private int GetDirectionMultiplier(Direction dir)
        {
            return dir switch
            {
                Direction.Left => -1,
                Direction.Right => 1,
                Direction.Up => -1,
                Direction.Down => 1,
                _ => 1
            };
        }

        private void MoveTowardsTarget(double targetX, double targetY, int[,] gameMap)
        {
            var directions = GetPossibleDirections(gameMap);
            Direction bestDirection = CurrentDirection;
            double minDistance = double.MaxValue;

            foreach (var dir in directions)
            {
                var (newX, newY) = CalculateNewPosition(dir);
                int intX = (int)newX;
                int intY = (int)newY;

                if (!IsValidMove(intX, intY, gameMap) || gameMap[intY, intX] == 1)
                {
                    continue;
                }

                double distance = Math.Sqrt(Math.Pow(newX - targetX, 2) + Math.Pow(newY - targetY, 2));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestDirection = dir;
                }
            }

            if (minDistance == double.MaxValue)
            {
                bestDirection = OppositeDirection(CurrentDirection);
            }

            MoveInDirection(bestDirection, gameMap);
        }

        private List<Direction> GetPossibleDirections(int[,] gameMap)
        {
            var directions = new List<Direction>();
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (dir == OppositeDirection(CurrentDirection)) continue;

                var (newX, newY) = CalculateNewPosition(dir);
                int intX = (int)newX;
                int intY = (int)newY;

                if (IsValidMove(intX, intY, gameMap))
                {
                    directions.Add(dir);
                }
            }
            return directions.Count > 0 ? directions : new List<Direction> { OppositeDirection(CurrentDirection) };
        }

        private Direction OppositeDirection(Direction dir)
        {
            return dir switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                Direction.Right => Direction.Left,
                _ => Direction.Up
            };
        }

        private void MoveRandomly(int[,] gameMap)
        {
            var validDirections = GetPossibleDirections(gameMap);
            if (validDirections.Count > 0)
            {
                int index = random.Next(validDirections.Count);
                MoveInDirection(validDirections[index], gameMap);
            }
        }

        private void MoveInDirection(Direction direction, int[,] gameMap)
        {
            CurrentDirection = direction;
            var (nextX, nextY) = CalculateNewPosition(CurrentDirection);
            int intX = (int)nextX;
            int intY = (int)nextY;

            if (IsValidMove(intX, intY, gameMap))
            {
                X = nextX;
                Y = nextY;
            }
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
            if (x < 0 || y < 0 || y >= map.GetLength(0) || x >= map.GetLength(1))
                return false;
            return map[y, x] == 0;
        }

        private (double X, double Y) GetScatterCorner(int[,] gameMap)
        {
            return Color switch
            {
                GhostColor.Red => (0, 0),
                GhostColor.Pink => (0, gameMap.GetLength(0) - 1),
                GhostColor.Blue => (gameMap.GetLength(1) - 1, 0),
                GhostColor.Orange => (gameMap.GetLength(1) - 1, gameMap.GetLength(0) - 1),
                _ => (0, 0)
            };
        }

        public void SetFrightened()
        {
            if (State != GhostState.Eaten)
            {
                State = GhostState.Frightened;
                frightenedTimer.Start();
            }
        }

        public void Reset()
        {
            X = SpawnPoint.X;
            Y = SpawnPoint.Y;
            State = GhostState.Scatter;
            CurrentDirection = Direction.Left;
            frightenedTimer.Stop();
        }
    }
}