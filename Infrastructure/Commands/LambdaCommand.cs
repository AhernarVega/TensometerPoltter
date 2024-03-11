using System;

using TensometerPoltter.Infrastructure.Commands.Base;

namespace TensometerPoltter.Infrastructure.Commands
{
    internal class LambdaCommand : BaseCommand
    {
        // Делегат действия команды
        private readonly Action<object> _execute;
        // Делегат проверки доступности команды
        private readonly Func<object, bool> _canExecute;
        // Конструктор с инициализацие делегатов
        public LambdaCommand(Action<object> execute, Func<object, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute!;
        }

        // Предопределенные функции
        // Если не передана команда проверки на возможность исполнения, то по умолчанию исполнение доступно
        public override bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter!) ?? true;

        // Исполняемый код команды
        public override void Execute(object? parameter) => _execute(parameter!);
    }
}
