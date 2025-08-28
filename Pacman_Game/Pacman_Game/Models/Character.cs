using System.ComponentModel;

namespace Pacman_Game.Models
{
    public abstract class Character : INotifyPropertyChanged
    {
        private double x;
        private double y;

        protected (double, double) CalculateNewPosition(Direction direction, double speed)
        {
            return direction switch
            {
                Direction.Up => (X, Y - speed),
                Direction.Down => (X, Y + speed),
                Direction.Left => (X - speed, Y),
                Direction.Right => (X + speed, Y),
                _ => (X, Y)
            };
        }
        protected bool IsValidMove(int x, int y, Map map)
        {
            return !map.IsBlocking(x, y);
        }

        // Posición X del personaje en el mapa.
        public double X
        {
            get => x;
            set
            {
                x = value;
                OnPropertyChanged(nameof(X));
            }
        }

        // Posición Y del personaje en el mapa.
        public double Y
        {
            get => y;
            set
            {
                y = value;
                OnPropertyChanged(nameof(Y));
            }
        }

        public Direction CurrentDirection { get; set; } = Direction.Right;
        public Direction NextDirection { get; set; } = Direction.Right;
        public int Speed { get; protected set; } = 1;  // Velocidad de movimiento del personaje.

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}