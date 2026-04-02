using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pacman_Game
{
    public class GameLoop : IDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly int _targetFrameTimeMs;
        private bool _paused;
        private bool _disposed;

        public event Action<double>? Update;
        public event EventHandler<CriticalErrorEventArgs>? CriticalError;

        public GameLoop(int frameTimeMs)
        {
            _targetFrameTimeMs = frameTimeMs;
        }

        public void Start()
        {
            if (_disposed) return;
            if (_loopTask != null && !_loopTask.IsCompleted)
                return;

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoop(_cts.Token));
        }

        public void Stop()
        {
            if (_disposed) return;

            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _loopTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException) { }

            lock (this)
            {
                if (_cts != null)
                {
                    try { _cts.Dispose(); } catch (ObjectDisposedException) { }
                    _cts = null;
                }
                _loopTask = null;
            }
        }

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        private void RunLoop(CancellationToken token)
        {
            DateTime lastFrameTime = DateTime.UtcNow;

            while (!token.IsCancellationRequested && !_disposed)
            {
                if (_paused)
                {
                    Thread.Sleep(50);
                    lastFrameTime = DateTime.UtcNow;
                    continue;
                }

                var currentTime = DateTime.UtcNow;
                var deltaTime = (currentTime - lastFrameTime).TotalSeconds;
                lastFrameTime = currentTime;

                // Limitar delta time máximo para evitar saltos grandes
                deltaTime = Math.Min(deltaTime, 0.1);

                try
                {
                    Update?.Invoke(deltaTime);
                }
                catch (OperationCanceledException)
                {
                    // Cancelación limpia, continuar el loop
                    Console.WriteLine("[GameLoop] Update cancelado limpiamente");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameLoop] Error en Update: {ex.GetType().Name} - {ex.Message}");
                    Console.WriteLine($"[GameLoop] Stack trace: {ex.StackTrace}");

                    // Notificar a los suscriptores del error crítico
                    CriticalError?.Invoke(this, new CriticalErrorEventArgs(ex));

                    // Detener el loop de forma segura
                    _paused = true;

                    // Esperar un momento para permitir que el ViewModel maneje el error
                    Thread.Sleep(100);

                    // Si el error fue crítico, podemos decidir si continuar o no
                    // Por ahora continuamos pero en estado pausado
                }

                var elapsed = (DateTime.UtcNow - currentTime).TotalMilliseconds;
                var sleep = _targetFrameTimeMs - elapsed;

                if (sleep > 0)
                {
                    try
                    {
                        Thread.Sleep((int)sleep);
                    }
                    catch (ThreadInterruptedException)
                    {
                        // Interrupción limpia durante shutdown
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            Stop();
        }
    }

    public class CriticalErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public CriticalErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}