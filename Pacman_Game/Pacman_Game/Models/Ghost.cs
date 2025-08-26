using System;
using System.Collections.Generic;
using Avalonia.Threading;
using ReactiveUI;

namespace Pacman_Game.Models
{
    // Enumeraciones para color
    public enum GhostColor { Red, Pink, Blue, Orange }

    // Enumeraciones Estados de los fantasmas
    public enum GhostState { LeavingHouse, Scatter, Chase, Frightened, Eaten }

    public abstract class Ghost : Character
    {
        // Propiedades
        public GhostColor Color { get; }
        public GhostState State { get; set; } = GhostState.LeavingHouse;
        public virtual double SpeedFactor { get; } = 0.85;
        public bool IsInTunnel { get; set; } = false;
        private Random random = new Random();

        // Temporizadores: Ciclo de estados, modo asustado y final de modo asustado
        private DispatcherTimer stateTimer;
        private DispatcherTimer frightenedTimer;
        private DispatcherTimer _frightenedEndingTimer;

        // SpawnPoint y HouseExit: Posiciones de inicio y salida de la casa
        protected (int X, int Y) SpawnPoint;
        protected (int X, int Y) HouseExit = (14, 11);
        private int _dotsRequiredToLeave;
        private static int _dotsEatenGlobal;

        protected Ghost(GhostColor color, double x, double y, int dotsRequiredToLeave)
        {
            Color = color;
            X = x;
            Y = y;
            Speed = 1;
            CurrentDirection = Direction.Left;
            _dotsRequiredToLeave = dotsRequiredToLeave;

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
        }

        public static void UpdateDotsEaten(int dotsEaten)
        {
            _dotsEatenGlobal = dotsEaten;
        }

        public double GetCurrentSpeed()
        {
            if (IsInTunnel) return 0.4;
            if (State == GhostState.Eaten) return 2.0;
            if (State == GhostState.Frightened) return 0.5;
            return 0.75;
        }

        // CycleStates: Alterna entre Scatter y Chase
        private void CycleStates()
        {
            if (State == GhostState.Scatter)
            {
                State = GhostState.Chase;
                stateTimer.Interval = TimeSpan.FromSeconds(20);
            }
            else if (State == GhostState.Chase)
            {
                State = GhostState.Scatter;
                if (stateTimer.Interval.TotalSeconds == 20)
                    stateTimer.Interval = TimeSpan.FromSeconds(7);
                else if (stateTimer.Interval.TotalSeconds == 7)
                    stateTimer.Interval = TimeSpan.FromSeconds(5);
                else if (stateTimer.Interval.TotalSeconds == 5)
                    stateTimer.Stop();
            }
        }

        // ChasePacman: Lógica de movimiento según el estado del fantasma
        public void ChasePacman(Pacman pacman, Map map)
        {
            if (State == GhostState.LeavingHouse && _dotsEatenGlobal >= _dotsRequiredToLeave)
            {
                State = GhostState.Scatter;
                stateTimer.Start();
            }

            switch (State)
            {
                case GhostState.LeavingHouse:
                    MoveTowardsTarget(HouseExit.X, HouseExit.Y, map);
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
                    MoveRandomly(map);
                    break;
                case GhostState.Eaten:
                    MoveTowardsTarget(SpawnPoint.X, SpawnPoint.Y, map);
                    if (Math.Abs(X - SpawnPoint.X) < 0.1 && Math.Abs(Y - SpawnPoint.Y) < 0.1)
                    {
                        State = GhostState.LeavingHouse;
                    }
                    break;
            }
        }

        // GetTargetPosition: Posición objetivo según estrategia de cada fantasma
        protected abstract (double X, double Y) GetTargetPosition(Pacman pacman);

        // GetScatterCorner: Retorna la esquina de dispersión según color y mapa.
        protected virtual (double X, double Y) GetScatterCorner(Map? map)
        {
            if (map == null)
            {
                return Color switch
                {
                    GhostColor.Red => (27, 0),
                    GhostColor.Pink => (0, 0),
                    GhostColor.Blue => (27, 30),
                    GhostColor.Orange => (0, 30),
                    _ => (0, 0)
                };
            }

            return Color switch
            {
                GhostColor.Red => (map.Width - 1, 0),
                GhostColor.Pink => (0, 0),
                GhostColor.Blue => (map.Width - 1, map.Height - 1),
                GhostColor.Orange => (0, map.Height - 1),
                _ => (0, 0)
            };
        }

        // MoveTowardsTarget: Calcula mejor dirección para acercarse a la posición objetivo.
        private void MoveTowardsTarget(double targetX, double targetY, Map map)
        {
            var directions = GetPossibleDirections(map);
            Direction bestDirection = CurrentDirection;
            double minDistance = double.MaxValue;

            foreach (var dir in directions)
            {
                var (newX, newY) = CalculateNewPosition(dir);
                double distance = Math.Sqrt(Math.Pow(newX - targetX, 2) + Math.Pow(newY - targetY, 2));

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestDirection = dir;
                }
            }

            MoveInDirection(bestDirection, map);
        }

        private List<Direction> GetPossibleDirections(Map map)
        {
            var directions = new List<Direction>();
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (dir == OppositeDirection(CurrentDirection) &&
                    State != GhostState.Frightened &&
                    State != GhostState.Eaten)
                {
                    continue;
                }

                var (newX, newY) = CalculateNewPosition(dir);
                int intX = (int)newX;
                int intY = (int)newY;

                if (IsValidMove(intX, intY, map) || IsInTunnel)
                {
                    directions.Add(dir);
                }
            }

 
            if (directions.Count == 0)
            {
                directions.Add(OppositeDirection(CurrentDirection));
            }

            return directions;
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

            if (IsValidMove(intX, intY, map) || IsInTunnel)
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
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            bestDir = dir;
                        }
                    }
                    CurrentDirection = bestDir;
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
            if ((y >= 13 && y <= 14) && (x < 0 || x >= map.Width))
                return true;

            if (x < 0 || y < 0 || y >= map.Height || x >= map.Width)
                return false;

            return !map.IsBlocking(x, y);
        }

        // SetFrightened: Activa modo asustado y reinicia temporizadores.
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
            State = GhostState.LeavingHouse;
            CurrentDirection = Direction.Left;
            frightenedTimer.Stop();
            stateTimer.Stop();
        }
    }

    // Blinky: Fantasma rojo, persigue directamente a Pac-Man.
    public class Blinky : Ghost
    {
        public Blinky(double x, double y) : base(GhostColor.Red, x, y, 0)
        {
            SpawnPoint = (14, 14);
        }

        protected override (double X, double Y) GetTargetPosition(Pacman pacman)
        {
            return (pacman.X, pacman.Y);
        }
    }

    // Pinky: Fantasma rosa, apunta a 4 casillas adelante de Pac-Man.
    public class Pinky : Ghost
    {
        public Pinky(double x, double y) : base(GhostColor.Pink, x, y, 0)
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

    // Inky: Fantasma azul, usa posición de Blinky y vector de Pac-Man para calcular objetivo.
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
            var (blinkyX, blinkyY) = (blinky.X, blinky.Y);
            var (pacmanAheadX, pacmanAheadY) = pacman.CurrentDirection switch
            {
                Direction.Left => (pacman.X - 2, pacman.Y),
                Direction.Right => (pacman.X + 2, pacman.Y),
                Direction.Up => (pacman.X, pacman.Y - 2),
                Direction.Down => (pacman.X, pacman.Y + 2),
                _ => (pacman.X, pacman.Y)
            };
            double vectorX = pacmanAheadX - blinkyX;
            double vectorY = pacmanAheadY - blinkyY;
            return (blinkyX + 2 * vectorX, blinkyY + 2 * vectorY);
        }
    }

    // Clyde: Fantasma naranja, alterna entre perseguir a Pac-Man o ir a esquina según distancia.
    public class Clyde : Ghost
    {
        public Clyde(double x, double y) : base(GhostColor.Orange, x, y, 60)
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
