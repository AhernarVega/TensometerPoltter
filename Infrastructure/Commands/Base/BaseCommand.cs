using System;
using System.Windows.Input;

namespace TensometerPoltter.Infrastructure.Commands.Base
{
    // Базовое определение команды
    internal abstract class BaseCommand : ICommand
    {
        // Событие при котором срабатывает команда
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        // Что происходит при исполнении команды
        public abstract bool CanExecute(object? parameter);

        // Доступна ли команда к исполнению
        public abstract void Execute(object? parameter);
    }
}
