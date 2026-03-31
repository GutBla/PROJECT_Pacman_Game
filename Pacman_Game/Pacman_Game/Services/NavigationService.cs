using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace Pacman_Game.Services
{
    public sealed class NavigationService
    {
        private static readonly Lazy<NavigationService> _instance =
            new(() => new NavigationService());

        public static NavigationService Instance => _instance.Value;

        private Window? _mainWindow;
        private readonly Stack<Window> _navigationStack = new();
        private readonly Dictionary<Type, object?> _navigationParameters = new();

        private NavigationService() { }

        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public void NavigateTo<T>(object? parameter = null) where T : Window, new()
        {
            var currentWindow = _navigationStack.Count > 0
                ? _navigationStack.Peek()
                : _mainWindow;

            var newWindow = new T();

            _navigationParameters[typeof(T)] = parameter;

            if (parameter != null && newWindow is INavigationAware aware)
                aware.OnNavigatedTo(parameter);

            _navigationStack.Push(newWindow);
            newWindow.Show();
            currentWindow?.Hide();

            newWindow.Closed += (_, _) =>
            {
                if (_navigationStack.Count > 0 && _navigationStack.Peek() == newWindow)
                    _navigationStack.Pop();
            };
        }

        public void GoBack()
        {
            if (_navigationStack.Count > 1)
            {
                var current = _navigationStack.Pop();
                _navigationStack.Peek().Show();
                current.Close();
            }
            else if (_navigationStack.Count == 1)
            {
                var current = _navigationStack.Pop();
                _mainWindow?.Show();
                current.Close();
            }
        }

        public void NavigateToMainMenu()
        {
            while (_navigationStack.Count > 0)
                _navigationStack.Pop().Close();

            _mainWindow?.Show();
        }

        public object? GetNavigationParameter<T>() where T : Window =>
            _navigationParameters.TryGetValue(typeof(T), out var param) ? param : null;

        public void ClearParameters() => _navigationParameters.Clear();
    }

    public interface INavigationAware
    {
        void OnNavigatedTo(object parameter);
    }
}