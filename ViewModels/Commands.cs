using System.Windows.Input;

namespace MilitarySupply.ViewModels
{
    // 버튼의 Command에 연결하는 동작 (항상 누를 수 있음)
    public sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }

    // CommandParameter 값을 받아서 실행하는 버전
    public sealed class ParameterCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }
}
