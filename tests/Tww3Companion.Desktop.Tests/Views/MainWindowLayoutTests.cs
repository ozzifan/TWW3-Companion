using System.Xml.Linq;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Views;

public sealed class MainWindowLayoutTests
{
  private static readonly string DesktopDirectory = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Tww3Companion.Desktop"));

  [Fact]
  public void MainWindowDeclaresAcceptedSizeAndPersistentThreeRegionGrid()
  {
    var document = XDocument.Load(Path.Combine(DesktopDirectory, "Views", "MainWindow.axaml"));
    var window = document.Root!;

    Assert.Equal("1280", window.Attribute("Width")?.Value);
    Assert.Equal("800", window.Attribute("Height")?.Value);
    Assert.Equal("1024", window.Attribute("MinWidth")?.Value);
    Assert.Equal("640", window.Attribute("MinHeight")?.Value);

    var shell = document.Descendants().Single(element => (string?)element.Attribute("Name") == "WorkspaceShell");
    var columns = shell.Elements().Single(element => element.Name.LocalName == "Grid.ColumnDefinitions").Elements().ToArray();
    Assert.Equal(3, columns.Length);
    Assert.Equal("208", columns[0].Attribute("Width")?.Value);
    Assert.Equal("*", columns[1].Attribute("Width")?.Value);
    Assert.Equal("384", columns[2].Attribute("Width")?.Value);
  }

  [Fact]
  public void ShellUsesAccessibleStandardControlsAndContainsOnlyApprovedRepresentativeActions()
  {
    var text = File.ReadAllText(Path.Combine(DesktopDirectory, "Views", "MainWindow.axaml"));

    Assert.Contains("AutomationProperties.Name=\"Workspace navigation\"", text);
    Assert.Contains("AutomationProperties.Name=\"Mod Library\"", text);
    Assert.Contains("AutomationProperties.Name=\"Return Home\"", text);
    Assert.Contains(":focus-visible", text);
    Assert.Contains("This Workspace contains no Mods or Collections yet. No data has been added.", text);
    Assert.DoesNotContain(">Import<", text, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(">Search<", text, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(">Profiles<", text, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(">Health<", text, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void CompatibilityViewExposesOnlyApprovedDecisionActions()
  {
    var text = File.ReadAllText(Path.Combine(DesktopDirectory, "Views", "CompatibilityView.axaml"));

    Assert.Contains("Exit", text);
    Assert.Contains("Continue Anyway", text);
    Assert.Contains("AutomationProperties.Name", text);
  }

  [Fact]
  public void RuntimeWiresDisplayCompatibilityAndThemeChanges()
  {
    var windowCode = File.ReadAllText(Path.Combine(DesktopDirectory, "Views", "MainWindow.axaml.cs"));
    var appCode = File.ReadAllText(Path.Combine(DesktopDirectory, "App.axaml.cs"));
    var compositionCode = File.ReadAllText(Path.Combine(DesktopDirectory, "Composition", "ApplicationComposition.cs"));

    Assert.DoesNotContain("Opened +=", windowCode);
    Assert.Contains("AttachTopLevel", appCode);
    Assert.Contains("EvaluateAttachedWorkArea", compositionCode);
    Assert.Contains("ColorValuesChanged", appCode);
    Assert.Contains("RequestedThemeVariant", appCode);
  }
}
