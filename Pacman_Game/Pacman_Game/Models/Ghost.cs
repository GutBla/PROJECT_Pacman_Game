using System;
using System.Collections.Generic;
using Avalonia.Threading;
using ReactiveUI;

namespace Pacman_Game.Models
{
    public enum GhostColor { Red, Pink, Blue, Orange }
    public enum GhostState { LeavingHouse, Scatter, Chase, Frightened, Eaten }

    public abstract class Ghost : Character
    {
        public GhostColor Color { get; }
        public GhostState State { get; set; } = GhostState.LeavingHouse;
        public virtual double SpeedFactor { get; } = 0.85;
        public bool IsInTunnel { get; set; } = false;

        private Random random = new Random();
        private int _stateTimer = 0;
        private int _scatterCount = 0;
        private const int MAX_SCATTER_CYCLES = 4;

        protected (int X, int Y) SpawnPoint;
        protected (int X, int Y) HouseExit = (14, 11);
        private int _dotsRequiredToLeave;
        private static int _dotsEatenGlobal;
        private bool _isFrightenedEnding = false;

        protected Ghost(GhostColor color, double x, double y, int dotsRequiredToLeave)
        {
            Color = color;
            X = x;
            Y = y;
            Speed = 1;
            CurrentDirection = Direction.Left;
            _dotsRequiredToLeave = dotsRequiredToLeave;
        }

        // Actualiza el contador global de puntos comidos
        public static void UpdateDotsEaten(int dotsEaten) => _dotsEatenGlobal = dotsEaten;

        // Retorna la velocidad actual según el estado
        public double GetCurrentSpeed()
        {
            if (IsInTunnel) return 0.4;
            if (State == GhostState.Eaten) return 2.0;
            if (State == GhostState.Frightened) return 0.5;
            return State == GhostState.Chase ? 0.85 : 0.75;
        }

        // Actualiza el estado del fantasma según temporizador y ciclos
        public void UpdateState()
        {
            if (State == GhostState.LeavingHouse && _dotsEatenGlobal >= _dotsRequiredToLeave)
            {
                State = GhostState.Scatter;
                _stateTimer = 0;
            }

            if (State == GhostState.Frightened)
            {
                if (_stateTimer >= 100) { State = GhostState.Chase; _stateTimer = 0; }
                else if (_stateTimer >= 80) _isFrightenedEnding = !_isFrightenedEnding;
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
                    if (_scatterCount >= MAX_SCATTER_CYCLES) State = GhostState.Chase;
                }
            }
        }

        private int GetScatterDuration() => _scatterCount switch { 0 => 70, 1 => 70, 2 => 50, 3 => 50, _ => 1 };
        private int GetChaseDuration() => _scatterCount switch { 0 => 200, 1 => 200, 2 => 1000, 3 => 1000, _ => 10000 };

        // Decide el movimiento según el estado
        public void ChasePacman(Pacman pacman, Map map)
        {
            UpdateState();
            switch (State)
            {
                case GhostState.LeavingHouse:
                    if (_dotsEatenGlobal >= _dotsRequiredToLeave)
                    {
                        MoveTowardsTarget(HouseExit.X, HouseExit.Y, map);
                        if (Math.Abs(X - HouseExit.X) < 0.1 && Math.Abs(Y - HouseExit.Y) < 0.1)
                        {
                            State = GhostState.Scatter;
                            _stateTimer = 0;
                        }
                    }
                    break;
                case GhostState.Chase:
                    var target = GetTargetPosition(pacman);
                    MoveTowardsTarget(target.X, target.Y, map);
                    break;
                case GhostState.Scatter:
                    var corner = GetScatterCorner(map);
                    MoveTowardsTarget(corner.X, corner.Y, map);
                    break;
                case GhostState.Frightened:
                    MoveRandomly(map, pacman);
                    break;
                case GhostState.Eaten:
                    MoveTowardsTarget(SpawnPoint.X, SpawnPoint.Y, map);
                    if (Math.Abs(X - SpawnPoint.X) < 0.1 && Math.Abs(Y - SpawnPoint.Y) < 0.1)
                        State = GhostState.LeavingHouse;
                    break;
            }
        }

        protected abstract (double X, double Y) GetTargetPosition(Pacman pacman);

        // Devuelve la esquina de scatter según color
        protected virtual (double X, double Y) GetScatterCorner(Map? map) =>
            map == null ? Color switch { GhostColor.Red => (27, 0), GhostColor.Pink => (0, 0), GhostColor.Blue => (27, 30), GhostColor.Orange => (0, 30), _ => (0, 0) }
                        : Color switch { GhostColor.Red => (map.Width - 1, 0), GhostColor.Pink => (0, 0), GhostColor.Blue => (map.Width - 1, map.Height - 1), GhostColor.Orange => (0, map.Height - 1), _ => (0, 0) };

        // Movimiento hacia un objetivo calculando la mejor dirección
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
                if (dir == CurrentDirection) { canContinueCurrent = true; if (distance < minDistance) { minDistance = distance; bestDirection = dir; } }
                else if (distance < minDistance) { minDistance = distance; bestDirection = dir; }
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

        // Retorna direcciones válidas según posición y estado
        private List<Direction> GetPossibleDirections(Map map)
        {
            var directions = new List<Direction>();
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (dir == OppositeDirection(CurrentDirection) && State != GhostState.Frightened && State != GhostState.Eaten)
                    continue;

                var (newX, newY) = CalculateNewPosition(dir);
                int intX = (int)Math.Round(newX);
                int intY = (int)Math.Round(newY);

                if (IsValidMove(intX, intY, map) || IsInTunnel) directions.Add(dir);
            }

            if (directions.Count == 0) directions.Add(OppositeDirection(CurrentDirection));
            return directions;
        }

        private Direction OppositeDirection(Direction dir) => dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => Direction.Up
        };

        // Movimiento aleatorio (estado Frightened)
        private void MoveRandomly(Map map, Pacman pacman)
        {
            var validDirections = GetPossibleDirections(map);
            if (validDirections.Count == 0) return;

            if (State == GhostState.Frightened && !_isFrightenedEnding)
            {
                var directionsByDistance = new List<(Direction dir, double distance)>();
                foreach (var dir in validDirections)
                {
                    var (newX, newY) = CalculateNewPosition(dir);
                    double distance = Math.Sqrt(Math.Pow(newX - pacman.X, 2) + Math.Pow(newY - pacman.Y, 2));
                    directionsByDistance.Add((dir, distance));
                }
                directionsByDistance.Sort((a, b) => b.distance.CompareTo(a.distance));
                int index = random.Next(Math.Min(2, directionsByDistance.Count));
                MoveInDirection(directionsByDistance[index].dir, map);
            }
            else
            {
                int index = random.Next(validDirections.Count);
                MoveInDirection(validDirections[index], map);
            }
        }

        private void MoveInDirection(Direction direction, Map map)
        {
            IsInTunnel = (Y >= 13 && Y <= 14) && (X < 1 || X > map.Width - 2);
            CurrentDirection = direction;
            double tolerance = 0.3;
            bool nearCenterX = Math.Abs(X - Math.Round(X)) < tolerance;
            bool nearCenterY = Math.Abs(Y - Math.Round(Y)) < tolerance;

            var (nextX, nextY) = CalculateNewPosition(CurrentDirection);
            int intNextX = (int)Math.Round(nextX);
            int intNextY = (int)Math.Round(nextY);

            if ((nearCenterX || nearCenterY) && (CurrentDirection == Direction.Left || CurrentDirection == Direction.Right) &&
                (NextDirection == Direction.Up || NextDirection == Direction.Down))
            {
                if (IsValidMove(intNextX, (int)Math.Round(Y), map) && IsValidMove((int)Math.Round(X), intNextY, map))
                {
                    X = nextX;
                    Y = CalculateNewPosition(NextDirection).Item2;
                    CurrentDirection = NextDirection;
                    return;
                }
            }

            if (IsValidMove(intNextX, intNextY, map) || IsInTunnel)
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
                var directions = GetPossibleDirections(map);
                if (directions.Count > 0)
                {
                    Direction bestDir = directions[0];
                    double minDistance = double.MaxValue;
                    foreach (var dir in directions)
                    {
                        var (testX, testY) = CalculateNewPosition(dir);
                        double distance = Math.Sqrt(Math.Pow(testX - nextX, 2) + Math.Pow(testY - nextY, 2));
                        if (distance < minDistance) { minDistance = distance; bestDir = dir; }
                    }
                    CurrentDirection = bestDir;
                    var (finalX, finalY) = CalculateNewPosition(bestDir);
                    if (IsValidMove((int)Math.Round(finalX), (int)Math.Round(finalY), map) || IsInTunnel)
                    {
                        X = finalX;
                        Y = finalY;
                        if (IsInTunnel) { if (X < 0) X = map.Width - 1; if (X >= map.Width) X = 0; }
                    }
                }
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
            if ((y >= 13 && y <= 14) && (x < 0 || x >= map.Width)) return true;
            if (x < 0 || y < 0 || y >= map.Height || x >= map.Width) return false;
            return !map.IsBlocking(x, y);
        }

        // Cambia el estado a Frightened
        public void SetFrightened()
        {
            if (State != GhostState.Eaten)
            {
                State = GhostState.Frightened;
                _stateTimer = 0;
                _isFrightenedEnding = false;
            }
        }

        // Resetea el fantasma a su posición inicial
        public void Reset()
        {
            X = SpawnPoint.X;
            Y = SpawnPoint.Y;
            State = GhostState.LeavingHouse;
            CurrentDirection = Direction.Left;
            _stateTimer = 0;
            _scatterCount = 0;
        }
    }

    public class Blinky : Ghost
    {
        public Blinky(double x, double y) : base(GhostColor.Red, x, y, 0) => SpawnPoint = (14, 14);
        protected override (double X, double Y) GetTargetPosition(Pacman pacman) => (pacman.X, pacman.Y);
    }

    public class Pinky : Ghost
    {
        public Pinky(double x, double y) : base(GhostColor.Pink, x, y, 0) => SpawnPoint = (14, 14);
        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            int f = 4, s = 4;
            return pacman.CurrentDirection switch
            {
                Direction.Left => (pacman.X - f, pacman.Y - s),
                Direction.Right => (pacman.X + f, pacman.Y - s),
                Direction.Up => (pacman.X - s, pacman.Y - f),
                Direction.Down => (pacman.X + s, pacman.Y + f),
                _ => (pacman.X, pacman.Y)
            };
        }
    }

    public class Inky : Ghost
    {
        private Blinky blinky;
        public Inky(double x, double y, Blinky blinky) : base(GhostColor.Blue, x, y, 30)
        {
            this.blinky = blinky;
            SpawnPoint = (14, 14);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            var (blX, blY) = (blinky.X, blinky.Y);
            var (pX, pY) = pacman.CurrentDirection switch
            {
                Direction.Left => (pacman.X - 2, pacman.Y),
                Direction.Right => (pacman.X + 2, pacman.Y),
                Direction.Up => (pacman.X, pacman.Y - 2),
                Direction.Down => (pacman.X, pacman.Y + 2),
                _ => (pacman.X, pacman.Y)
            };
            double tX = blX + 2 * (pX - blX), tY = blY + 2 * (pY - blY);
            double d = Math.Sqrt(Math.Pow(tX - pacman.X, 2) + Math.Pow(tY - pacman.Y, 2));
            if (d > 16) { double r = 16 / d; tX = pacman.X + (tX - pacman.X) * r; tY = pacman.Y + (tY - pacman.Y) * r; }
            return (tX, tY);
        }
    }

    public class Clyde : Ghost
    {
        public Clyde(double x, double y) : base(GhostColor.Orange, x, y, 60) => SpawnPoint = (14, 14);
        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            double distance = Math.Sqrt(Math.Pow(X - pacman.X, 2) + Math.Pow(Y - pacman.Y, 2));
            if (distance < 8) return (pacman.X, pacman.Y);
            var corner = GetScatterCorner(null);
            double pull = 0.3;
            return (corner.X * (1 - pull) + pacman.X * pull, corner.Y * (1 - pull) + pacman.Y * pull);
        }
    }
}
