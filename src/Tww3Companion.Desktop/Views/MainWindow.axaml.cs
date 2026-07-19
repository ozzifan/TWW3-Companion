using Avalonia.Controls;
using Tww3Companion.Desktop.ViewModels;

namespace Tww3Companion.Desktop.Views;

public partial class MainWindow : Window
{
  public MainWindow() : this(new ShellViewModel())
  {
  }

  public MainWindow(ShellViewModel viewModel)
  {
    InitializeComponent();
    DataContext = viewModel;
  }
}
