using System;
using System.Collections.Generic;

namespace Pacman_Game.Models
{
    public enum GhostColor { Red, Pink, Blue, Orange }
    public enum GhostState { LeavingHouse, Scatter, Chase, Frightened, FlashingFrightened, Eaten }

    public abstract class Ghost : Character
    {
        private readonly Random _random = new();
        private int _stateTimer;
        private int _scatterCount;
        private const int MaxScatterCycles = 4;
        protected (int X, int Y) SpawnPoint;
        protected (int X, int Y) HouseExit = (14, 11);
        private readonly int _dotsRequiredToLeave;
        private static int _dotsEatenGlobal;

        public event EventHandler? ReturnedHome;

        public GhostColor Color { get; }
        public GhostState State { get; set; } = GhostState.LeavingHouse;
        public virtual double SpeedFactor { get; } = 0.85;
        public bool IsInTunnel { get; set; }

        protected Ghost(GhostColor color, double x, double y, int dotsRequiredToLeave)
        {
            Color = color;
            X = x;
            Y = y;
            Speed = 1;
            CurrentDirection = Direction.Left;
            _dotsRequiredToLeave = dotsRequiredToLeave;
        }

        public static void UpdateDotsEaten(int dotsEaten) => _dotsEatenGlobal = dotsEaten;

        public double GetCurrentSpeed()
        {
            if (IsInTunnel) return 0.4;
            if (State == GhostState.Eaten) return 2.5;
            if (State == GhostState.Frightened || State == GhostState.FlashingFrightened) return 0.5;
            return State == GhostState.Chase ? 0.85 : 0.75;
        }

        public void UpdateState()
        {
            if (State == GhostState.LeavingHouse && _dotsEatenGlobal >= _dotsRequiredToLeave)
            {
                State = GhostState.Scatter;
                _stateTimer = 0;
            }

            if (State == GhostState.Frightened || State == GhostState.FlashingFrightened)
            {
                _stateTimer++;
                if (_stateTimer >= 100) { State = GhostState.Chase; _stateTimer = 0; }
                else if (_stateTimer >= 80 && State == GhostState.Frightened)
                    State = GhostState.FlashingFrightened;
            }
            else if (State == GhostState.Scatter || State == GhostState.Chase)
            {
                _stateTimer++;
                if (State == GhostState.Scatter && _stateTimer >= GetScatterDuration())
                {
                    State = GhostState.Chase;
                    _stateTimer = 0;
                }
                else if (State == GhostState.Chase && _stateTimer >= GetChaseDuration())
                {
                    State = GhostState.Scatter;
                    _stateTimer = 0;
                    _scatterCount++;
                    if (_scatterCount >= MaxScatterCycles) State = GhostState.Chase;
                }
            }
        }

        private int GetScatterDuration() => _scatterCount switch
        {
            0 or 1 => 70,
            2 or 3 => 50,
            _ => 1
        };

        private int GetChaseDuration() => _scatterCount switch
        {
            0 or 1 => 200,
            2 or 3 => 1000,
            _ => 10000
        };

        public void ChasePacman(Pacman pacman, Map map)
        {
            UpdateState();
            switch (State)
            {
                case GhostState.LeavingHouse:
                    if (_dotsEatenGlobal >= _dotsRequiredToLeave)
                    {
                        MoveTowardsTarget(HouseExit.X, HouseExit.Y, map);
                        if (Math.Abs(X - HouseExit.X) < 0.5 && Math.Abs(Y - HouseExit.Y) < 0.5)
                        {
                            State = GhostState.Scatter;
                            _stateTimer = 0;
                            X = HouseExit.X;
                            Y = HouseExit.Y;
                        }
                    }
                    break;
                case GhostState.Chase:
                    var chase = GetTargetPosition(pacman);
                    MoveTowardsTarget(chase.X, chase.Y, map);
                    break;
                case GhostState.Scatter:
                    var corner = GetScatterCorner(map);
                    MoveTowardsTarget(corner.X, corner.Y, map);
                    break;
                case GhostState.Frightened:
                case GhostState.FlashingFrightened:
                    MoveRandomly(map, pacman);
                    break;
                case GhostState.Eaten:
                    MoveTowardsTarget(SpawnPoint.X, SpawnPoint.Y, map);
                    double dist = Math.Sqrt(Math.Pow(X - SpawnPoint.X, 2) + Math.Pow(Y - SpawnPoint.Y, 2));
                    if (dist < 0.5)
                    {
                        X = SpawnPoint.X;
                        Y = SpawnPoint.Y;
                        State = GhostState.LeavingHouse;
                        _stateTimer = 0;
                        ReturnedHome?.Invoke(this, EventArgs.Empty);
                    }
                    break;
            }
        }

        protected abstract (double X, double Y) GetTargetPosition(Pacman pacman);

        protected virtual (double X, double Y) GetScatterCorner(Map? map) =>
            map == null
                ? Color switch
                {
                    GhostColor.Red => (27, 0),
                    GhostColor.Pink => (0, 0),
                    GhostColor.Blue => (27, 30),
                    GhostColor.Orange => (0, 30),
                    _ => (0, 0)
                }
                : Color switch
                {
                    GhostColor.Red => (map.Width - 1, 0),
                    GhostColor.Pink => (0, 0),
                    GhostColor.Blue => (map.Width - 1, map.Height - 1),
                    GhostColor.Orange => (0, map.Height - 1),
                    _ => (0, 0)
                };

        public void Reset()
        {
            X = SpawnPoint.X;
            Y = SpawnPoint.Y;
            State = GhostState.LeavingHouse;
            CurrentDirection = Direction.Left;
            _stateTimer = 0;
            _scatterCount = 0;
            IsInTunnel = false;
        }

        public void SetFrightened()
        {
            if (State != GhostState.Eaten && State != GhostState.LeavingHouse)
            {
                State = GhostState.Frightened;
                _stateTimer = 0;
            }
        }

        private void MoveTowardsTarget(double targetX, double targetY, Map map)
        {
            var directions = GetPossibleDirections(map);
            if (directions.Count == 1) { MoveInDirection(directions[0], map); return; }

            Direction bestDirection = CurrentDirection;
            double minDistance = double.MaxValue;
            bool canContinueCurrent = false;

            foreach (var dir in directions)
            {
                var (newX, newY) = CalculateNewPosition(dir);
                double distance = Math.Sqrt(Math.Pow(newX - targetX, 2) + Math.Pow(newY - targetY, 2));
                if (dir == CurrentDirection)
                {
                    canContinueCurrent = true;
                    if (distance < minDistance) { minDistance = distance; bestDirection = dir; }
                }
                else if (distance < minDistance)
                {
                    minDistance = distance;
                    bestDirection = dir;
                }
            }

            if (!canContinueCurrent && directions.Contains(OppositeDirection(CurrentDirection)))
            {
                directions.Remove(OppositeDirection(CurrentDirection));
                if (directions.Count > 0)
                {
                    bestDirection = directions[0];
                    foreach (var dir in directions)
                    {
                        var (newX, newY) = CalculateNewPosition(dir);
                        double distance = Math.Sqrt(Math.Pow(newX - targetX, 2) + Math.Pow(newY - targetY, 2));
                        if (distance < minDistance) { minDistance = distance; bestDirection = dir; }
                    }
                }
            }

            MoveInDirection(bestDirection, map);
        }

        private List<Direction> GetPossibleDirections(Map map)
        {
            List<Direction> directions = [];
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (dir == OppositeDirection(CurrentDirection) &&
                    State != GhostState.Frightened &&
                    State != GhostState.FlashingFrightened &&
                    State != GhostState.Eaten)
                    continue;

                var (newX, newY) = CalculateNewPosition(dir);
                int ix = (int)Math.Round(newX);
                int iy = (int)Math.Round(newY);
                if (GhostIsValidMove(ix, iy, map) || IsInTunnel)
                    directions.Add(dir);
            }
            if (directions.Count == 0)
                directions.Add(OppositeDirection(CurrentDirection));
            return directions;
        }

        private static Direction OppositeDirection(Direction dir) => dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => Direction.Up
        };

        private void MoveRandomly(Map map, Pacman pacman)
        {
            var validDirections = GetPossibleDirections(map);
            if (validDirections.Count == 0) return;

            if (State == GhostState.Frightened)
            {
                List<(Direction dir, double distance)> byDistance = [];
                foreach (var dir in validDirections)
                {
                    var (nx, ny) = CalculateNewPosition(dir);
                    double d = Math.Sqrt(Math.Pow(nx - pacman.X, 2) + Math.Pow(ny - pacman.Y, 2));
                    byDistance.Add((dir, d));
                }
                byDistance.Sort((a, b) => b.distance.CompareTo(a.distance));
                MoveInDirection(byDistance[_random.Next(Math.Min(2, byDistance.Count))].dir, map);
            }
            else
            {
                MoveInDirection(validDirections[_random.Next(validDirections.Count)], map);
            }
        }

        private void MoveInDirection(Direction direction, Map map)
        {
            IsInTunnel = (Y >= 13 && Y <= 14) && (X < 1 || X > map.Width - 2);
            CurrentDirection = direction;

            const double tolerance = 0.3;
            bool nearCenterX = Math.Abs(X - Math.Round(X)) < tolerance;
            bool nearCenterY = Math.Abs(Y - Math.Round(Y)) < tolerance;

            var (nextX, nextY) = CalculateNewPosition(CurrentDirection);
            int intNextX = (int)Math.Round(nextX);
            int intNextY = (int)Math.Round(nextY);

            if ((nearCenterX || nearCenterY) &&
                (CurrentDirection == Direction.Left || CurrentDirection == Direction.Right) &&
                (NextDirection == Direction.Up || NextDirection == Direction.Down))
            {
                if (GhostIsValidMove(intNextX, (int)Math.Round(Y), map) &&
                    GhostIsValidMove((int)Math.Round(X), intNextY, map))
                {
                    X = nextX;
                    Y = CalculateNewPosition(NextDirection).Item2;
                    CurrentDirection = NextDirection;
                    return;
                }
            }

            if (GhostIsValidMove(intNextX, intNextY, map) || IsInTunnel)
            {
                X = nextX;
                Y = nextY;
                if (IsInTunnel)
                {
                    if (X < 0) X = map.Width - 1;
                    if (X >= map.Width) X = 0;
                }
            }
            else
            {
                var dirs = GetPossibleDirections(map);
                if (dirs.Count > 0)
                {
                    Direction bestDir = dirs[0];
                    double minDist = double.MaxValue;
                    foreach (var dir in dirs)
                    {
                        var (tx, ty) = CalculateNewPosition(dir);
                        double d = Math.Sqrt(Math.Pow(tx - nextX, 2) + Math.Pow(ty - nextY, 2));
                        if (d < minDist) { minDist = d; bestDir = dir; }
                    }
                    CurrentDirection = bestDir;
                    var (fx, fy) = CalculateNewPosition(bestDir);
                    if (GhostIsValidMove((int)Math.Round(fx), (int)Math.Round(fy), map) || IsInTunnel)
                    {
                        X = fx;
                        Y = fy;
                        if (IsInTunnel)
                        {
                            if (X < 0) X = map.Width - 1;
                            if (X >= map.Width) X = 0;
                        }
                    }
                }
            }
        }

        private (double, double) CalculateNewPosition(Direction direction)
            => CalculateNewPosition(direction, GetCurrentSpeed());

        private static bool GhostIsValidMove(int x, int y, Map map)
        {
            if (y >= 13 && y <= 14 && (x < 0 || x >= map.Width)) return true;
            if (x < 0 || y < 0 || y >= map.Height || x >= map.Width) return false;
            return !map.IsBlocking(x, y);
        }
    }

    public class Blinky : Ghost
    {
        public Blinky(double x, double y) : base(GhostColor.Red, x, y, 0)
            => SpawnPoint = ((int)x, (int)y);
        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
            => (pacman.X, pacman.Y);
    }

    public class Pinky : Ghost
    {
        public Pinky(double x, double y) : base(GhostColor.Pink, x, y, 7)
            => SpawnPoint = ((int)x, (int)y);
        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            const int offset = 4;
            return pacman.CurrentDirection switch
            {
                Direction.Left => (pacman.X - offset, pacman.Y - offset),
                Direction.Right => (pacman.X + offset, pacman.Y - offset),
                Direction.Up => (pacman.X - offset, pacman.Y - offset),
                Direction.Down => (pacman.X + offset, pacman.Y + offset),
                _ => (pacman.X, pacman.Y)
            };
        }
    }

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

    public class Clyde : Ghost
    {
        public Clyde(double x, double y) : base(GhostColor.Orange, x, y, 60)
            => SpawnPoint = ((int)x, (int)y);
        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            double distance = Math.Sqrt(Math.Pow(X - pacman.X, 2) + Math.Pow(Y - pacman.Y, 2));
            if (distance < 8) return (pacman.X, pacman.Y);
            var corner = GetScatterCorner(null);
            const double pull = 0.3;
            return (corner.X * (1 - pull) + pacman.X * pull, corner.Y * (1 - pull) + pacman.Y * pull);
        }
    }
}