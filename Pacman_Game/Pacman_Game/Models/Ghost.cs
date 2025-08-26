using System;
using System.Collections.Generic;
using Avalonia.Threading;
using ReactiveUI;

namespace Pacman_Game.Models
{
    public enum GhostColor { Red, Pink, Blue, Orange }
    public enum GhostState { Chase, Scatter, Frightened, Eaten }

    public abstract class Ghost : Character
    {
        public GhostColor Color { get; }
        public GhostState State { get; set; } = GhostState.Scatter;

        public virtual double SpeedFactor { get; } = 0.85;
        public bool IsInTunnel { get; set; } = false;

        private Random random = new Random();
        private DispatcherTimer stateTimer;
        private DispatcherTimer frightenedTimer;
        private DispatcherTimer _frightenedEndingTimer;
        protected (int X, int Y) SpawnPoint;

        protected Ghost(GhostColor color, double x, double y)
        {
            Color = color;
            X = x;
            Y = y;
            Speed = 1;
            CurrentDirection = Direction.Left;
            stateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(7)
            };
            stateTimer.Tick += (s, e) => CycleStates();

            frightenedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            frightenedTimer.Tick += (s, e) =>
            {
                if (State == GhostState.Frightened)
                    State = GhostState.Chase;
            };

            _frightenedEndingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8)
            };
            _frightenedEndingTimer.Tick += (s, e) => { };

            stateTimer.Start();
        }

        public double GetCurrentSpeed()
        {
            if (IsInTunnel) return 0.4;
            if (State == GhostState.Eaten) return 2.0;
            if (State == GhostState.Frightened) return 0.5;
            return 0.75;
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

        public void ChasePacman(Pacman pacman, Map map)
        {
            switch (State)
            {
                case GhostState.Chase:
                    var target = GetTargetPosition(pacman);
                    MoveTowardsTarget(target.X, target.Y, map);
                    break;
                case GhostState.Scatter:
                    var corner = GetScatterCorner(map);
                    MoveTowardsTarget(corner.X, corner.Y, map);
                    break;
                case GhostState.Frightened:
                    MoveRandomly(map);
                    break;
                case GhostState.Eaten:
                    MoveTowardsTarget(SpawnPoint.X, SpawnPoint.Y, map);
                    if (Math.Abs(X - SpawnPoint.X) < 0.1 && Math.Abs(Y - SpawnPoint.Y) < 0.1)
                    {
                        State = GhostState.Scatter;
                    }
                    break;
            }
        }

        protected abstract (double X, double Y) GetTargetPosition(Pacman pacman);

        protected virtual (double X, double Y) GetScatterCorner(Map? map)
        {
            if (map == null)
            {
                return Color switch
                {
                    GhostColor.Red => (0, 0),
                    GhostColor.Pink => (0, 27),
                    GhostColor.Blue => (27, 0),
                    GhostColor.Orange => (27, 30),
                    _ => (0, 0)
                };
            }
            return Color switch
            {
                GhostColor.Red => (0, 0),
                GhostColor.Pink => (0, map.Height - 1),
                GhostColor.Blue => (map.Width - 1, 0),
                GhostColor.Orange => (map.Width - 1, map.Height - 1),
                _ => (0, 0)
            };
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

        private void MoveTowardsTarget(double targetX, double targetY, Map map)
        {
            var directions = GetPossibleDirections(map);
            Direction bestDirection = CurrentDirection;
            double minDistance = double.MaxValue;

            foreach (var dir in directions)
            {
                var (newX, newY) = CalculateNewPosition(dir);
                int intX = (int)Math.Round(newX);
                int intY = (int)Math.Round(newY);

                if (!IsValidMove(intX, intY, map) || map.IsBlocking(intX, intY))
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

            MoveInDirection(bestDirection, map);
        }

        private List<Direction> GetPossibleDirections(Map map)
        {
            var directions = new List<Direction>();
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (dir == OppositeDirection(CurrentDirection)) continue;
                var (newX, newY) = CalculateNewPosition(dir);
                int intX = (int)newX;
                int intY = (int)newY;
                if (IsValidMove(intX, intY, map))
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

        private void MoveRandomly(Map map)
        {
            var validDirections = GetPossibleDirections(map);
            if (validDirections.Count > 0)
            {
                int index = random.Next(validDirections.Count);
                MoveInDirection(validDirections[index], map);
            }
        }

        private void MoveInDirection(Direction direction, Map map)
        {
            IsInTunnel = (Y >= 13 && Y <= 14) && (X < 1 || X > map.Width - 2);
            CurrentDirection = direction;

            var (nextX, nextY) = CalculateNewPosition(CurrentDirection);
            int intX = (int)Math.Round(nextX);
            int intY = (int)Math.Round(nextY);

            if (IsValidMove(intX, intY, map))
            {
                X = nextX;
                Y = nextY;
            }
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

        private new bool IsValidMove(int x, int y, Map map)
        {
            if (x < 0 || y < 0 || y >= map.Height || x >= map.Width)
                return false;
            return !map.IsBlocking(x, y);
        }

        public void SetFrightened()
        {
            if (State != GhostState.Eaten)
            {
                State = GhostState.Frightened;
                frightenedTimer.Stop();
                frightenedTimer.Start();
                _frightenedEndingTimer.Stop();
                _frightenedEndingTimer.Start();
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

    public class Blinky : Ghost
    {
        public Blinky(double x, double y) : base(GhostColor.Red, x, y)
        {
            SpawnPoint = (14, 14);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            return (pacman.X, pacman.Y);
        }
    }

    public class Pinky : Ghost
    {
        public Pinky(double x, double y) : base(GhostColor.Pink, x, y)
        {
            SpawnPoint = (14, 14);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            int multiplier = 4;
            var (targetX, targetY) = pacman.CurrentDirection switch
            {
                Direction.Left => (pacman.X - multiplier, pacman.Y),
                Direction.Right => (pacman.X + multiplier, pacman.Y),
                Direction.Up => (pacman.X, pacman.Y - multiplier),
                Direction.Down => (pacman.X, pacman.Y + multiplier),
                _ => (pacman.X, pacman.Y)
            };
            return (targetX, targetY);
        }
    }

    public class Inky : Ghost
    {
        private Blinky blinky;

        public Inky(double x, double y, Blinky blinky) : base(GhostColor.Blue, x, y)
        {
            this.blinky = blinky;
            SpawnPoint = (14, 14);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            int offset = 2;
            var (blinkyX, blinkyY) = (blinky.X, blinky.Y);
            var (targetX, targetY) = (pacman.X + (pacman.X - blinkyX), pacman.Y + (pacman.Y - blinkyY));
            return (targetX * offset, targetY * offset);
        }
    }

    public class Clyde : Ghost
    {
        public Clyde(double x, double y) : base(GhostColor.Orange, x, y)
        {
            SpawnPoint = (14, 14);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            double distance = Math.Sqrt(Math.Pow(X - pacman.X, 2) + Math.Pow(Y - pacman.Y, 2));
            return distance > 8 ? (pacman.X, pacman.Y) : GetScatterCorner(null);
        }
    }
}