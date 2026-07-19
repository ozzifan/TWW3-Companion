using System.Xml.Linq;
using Xunit;

namespace Tww3Companion.Application.Tests.Architecture;

public sealed class DependencyRulesTests
{
  [Fact]
  public void Production_projects_follow_approved_dependency_rules()
  {
    var allowedProjectReferences = new Dictionary<string, string[]>
    {
      ["Tww3Companion.Domain"] = [],
      ["Tww3Companion.Application"] = ["Tww3Companion.Domain"],
      ["Tww3Companion.Infrastructure"] = ["Tww3Companion.Application", "Tww3Companion.Domain"],
      ["Tww3Companion.Desktop"] = ["Tww3Companion.Application", "Tww3Companion.Domain", "Tww3Companion.Infrastructure"]
    };

    var forbiddenPackages = new Dictionary<string, string[]>
    {
      ["Tww3Companion.Domain"] = ["Avalonia", "Microsoft.Data.Sqlite", "Microsoft.Extensions.Logging", "Serilog"],
      ["Tww3Companion.Application"] = ["Avalonia", "Microsoft.Data.Sqlite", "Serilog"]
    };

    var repositoryRoot = FindRepositoryRoot();

    foreach (var (projectName, allowedReferences) in allowedProjectReferences)
    {
      var projectFile = Path.Combine(repositoryRoot.FullName, "src", projectName, $"{projectName}.csproj");
      var project = XDocument.Load(projectFile);
      var actualReferences = project.Descendants("ProjectReference")
          .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
          .Order(StringComparer.Ordinal)
          .ToArray();

      Assert.Equal(allowedReferences.Order(StringComparer.Ordinal), actualReferences);

      if (!forbiddenPackages.TryGetValue(projectName, out var forbiddenPrefixes))
      {
        continue;
      }

      var packageReferences = project.Descendants("PackageReference")
          .Select(reference => reference.Attribute("Include")!.Value);

      foreach (var packageReference in packageReferences)
      {
        Assert.DoesNotContain(
            forbiddenPrefixes,
            prefix => packageReference.Equals(prefix, StringComparison.Ordinal)
                || packageReference.StartsWith($"{prefix}.", StringComparison.Ordinal));
      }
    }
  }

  private static DirectoryInfo FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
      {
        return directory;
      }
    }

    throw new DirectoryNotFoundException("Could not locate the repository root.");
  }
}
