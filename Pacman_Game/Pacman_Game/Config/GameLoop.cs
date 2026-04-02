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
        private volatile bool _paused; // Thread-safe para acceso desde UI y background
        private volatile bool _disposed;
        private readonly object _lockObj = new();

        public event Action<double>? Update;
        public event EventHandler<CriticalErrorEventArgs>? CriticalError;

        public GameLoop(int frameTimeMs)
        {
            _targetFrameTimeMs = frameTimeMs;
        }

        public void Start()
        {
            if (_disposed) return;
            lock (_lockObj)
            {
                if (_loopTask != null && !_loopTask.IsCompleted) return;
                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => RunLoop(_cts.Token), _cts.Token);
            }
        }

        public void Stop()
        {
            if (_disposed) return;
            CancellationTokenSource? cts;
            lock (_lockObj)
            {
                cts = _cts;
                _cts = null;
            }

            cts?.Cancel();
            try
            {
                _loopTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException) { }
            catch (OperationCanceledException) { }

            _loopTask = null;
        }

        public void Pause() => _paused = true;
        public void Resume() => _paused = false;

        private void RunLoop(CancellationToken token)
        {
            DateTime lastFrameTime = DateTime.UtcNow;
            while (!token.IsCancellationRequested && !_disposed)
            {
                if (_paused)
                {
                    Thread.Sleep(50);
                    lastFrameTime = DateTime.UtcNow; // Resetear delta tras pausa
                    continue;
                }

                var currentTime = DateTime.UtcNow;
                var deltaTime = Math.Min((currentTime - lastFrameTime).TotalSeconds, 0.1);
                lastFrameTime = currentTime;

                try
                {
                    Update?.Invoke(deltaTime);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    CriticalError?.Invoke(this, new CriticalErrorEventArgs(ex));
                    _paused = true;
                    Thread.Sleep(100);
                }

                var elapsed = (DateTime.UtcNow - currentTime).TotalMilliseconds;
                var sleep = _targetFrameTimeMs - elapsed;
                if (sleep > 0)
                {
                    try { Thread.Sleep((int)sleep); }
                    catch (ThreadInterruptedException) { }
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
        public CriticalErrorEventArgs(Exception exception) => Exception = exception;
    }
}