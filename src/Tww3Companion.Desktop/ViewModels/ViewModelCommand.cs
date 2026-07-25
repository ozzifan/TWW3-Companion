using System.Windows.Input;

namespace Tww3Companion.Desktop.ViewModels;

public sealed class ViewModelCommand : ICommand
{
  private readonly Action<object?> execute;
  private readonly Func<object?, bool>? canExecute;

  public ViewModelCommand(Action execute, Func<bool>? canExecute = null)
      : this(_ => execute(), canExecute is null ? null : _ => canExecute())
  {
  }

  public ViewModelCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
  {
    this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    this.canExecute = canExecute;
  }

  public event EventHandler? CanExecuteChanged;

  public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

  public void Execute(object? parameter) => execute(parameter);

  public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
