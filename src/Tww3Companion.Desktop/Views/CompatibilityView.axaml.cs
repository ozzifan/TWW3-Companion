using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Tww3Companion.Desktop.Views;

public partial class CompatibilityView : UserControl
{
  public CompatibilityView() => InitializeComponent();

  private void Exit_Click(object? sender, RoutedEventArgs e) =>
      (TopLevel.GetTopLevel(this) as Window)?.Close();
}
