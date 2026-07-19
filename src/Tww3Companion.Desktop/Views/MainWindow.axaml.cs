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
        Opened += (_, _) => EvaluateWorkArea();
    }

    private void EvaluateWorkArea()
    {
        var primary = Screens.Primary;
        if (primary is null || DataContext is not ShellViewModel viewModel)
        {
            return;
        }

        viewModel.EvaluateWorkArea(
            primary.WorkingArea.Width / primary.Scaling,
            primary.WorkingArea.Height / primary.Scaling);
    }
}
