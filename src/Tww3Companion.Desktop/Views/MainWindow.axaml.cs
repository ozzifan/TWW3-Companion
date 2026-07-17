using Avalonia.Controls;
using Tww3Companion.Desktop.ViewModels;

namespace Tww3Companion.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel();
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
