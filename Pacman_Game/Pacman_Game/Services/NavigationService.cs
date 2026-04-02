using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Pacman_Game.Services
{
    public sealed class NavigationService
    {
        private static readonly Lazy<NavigationService> _instance = new(() => new NavigationService());
        public static NavigationService Instance => _instance.Value;

        private Window? _currentWindow;
        private readonly Stack<Window> _windowStack = new();
        private readonly Dictionary<Type, object?> _parameters = new();
        private readonly object _lock = new();

        private NavigationService() { }

        public void SetCurrentWindow(Window window)
        {
            lock (_lock)
            {
                _currentWindow = window;
                if (_windowStack.Count == 0 || _windowStack.Peek() != window)
                    _windowStack.Push(window);
            }
        }

        public void NavigateTo<T>(object? parameter = null) where T : Window, new()
        {
            Dispatcher.UIThread.Post(() =>
            {
                lock (_lock)
                {
                    var newWindow = new T();
                    _parameters[typeof(T)] = parameter;
                    if (parameter != null && newWindow is INavigationAware aware)
                        aware.OnNavigatedTo(parameter);

                    var oldWindow = _currentWindow;
                    _currentWindow = newWindow;
                    _windowStack.Push(newWindow);

                    newWindow.Show();
                    oldWindow?.Close();
                }
            });
        }

        public async Task<T?> ShowModal<T>(object? parameter = null) where T : Window, new()
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                lock (_lock)
                {
                    var dialog = new T();
                    _parameters[typeof(T)] = parameter;
                    if (parameter != null && dialog is INavigationAware aware)
                        aware.OnNavigatedTo(parameter);

                    // No cerrar la ventana actual, solo mostrar modal
                    return dialog.ShowDialog<T?>(_currentWindow);
                }
            });
        }

        public void GoBack()
        {
            Dispatcher.UIThread.Post(() =>
            {
                lock (_lock)
                {
                    if (_windowStack.Count > 1)
                    {
                        var current = _windowStack.Pop();
                        var previous = _windowStack.Peek();
                        _currentWindow = previous;
                        current.Close();
                        previous.Show();
                    }
                    else if (_windowStack.Count == 1)
                    {
                        var current = _windowStack.Pop();
                        _currentWindow = null;
                        current.Close();
                    }
                }
            });
        }

        public void NavigateToMainMenu()
        {
            Dispatcher.UIThread.Post(() =>
            {
                lock (_lock)
                {
                    while (_windowStack.Count > 1)
                        _windowStack.Pop().Close();

                    if (_windowStack.Count == 1)
                    {
                        var main = _windowStack.Peek();
                        _currentWindow = main;
                        main.Show();
                    }
                }
            });
        }

        public object? GetParameter<T>() where T : Window
        {
            lock (_lock)
            {
                return _parameters.GetValueOrDefault(typeof(T));
            }
        }

        public void ClearParameters() => _parameters.Clear();
    }

    public interface INavigationAware
    {
        void OnNavigatedTo(object parameter);
    }
}