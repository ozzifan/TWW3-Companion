using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class ModLibraryViewModelTests
{
  [Fact]
  public void LoadPopulatesModItemsAndInspectorStartsEmpty()
  {
    var subject = new ModLibraryViewModel();

    subject.Load(
        [
            new ModLibraryItem("mod-1", "Alpha Mod", WorkshopId: "111", Author: "Alice", SourceReference: "steam://111"),
            new ModLibraryItem("mod-2", "Beta Mod", WorkshopId: "222", Author: "Bob", SourceReference: "steam://222")
        ]);

    Assert.Equal(2, subject.Mods.Count);
    Assert.Equal("Alpha Mod", subject.Mods[0].DisplayName);
    Assert.Equal("No mod selected", subject.Inspector.DisplayName);
    Assert.False(subject.Inspector.HasSelection);
  }

  [Fact]
  public void SelectingAModUpdatesTheDetailInspector()
  {
    var subject = new ModLibraryViewModel();

    subject.Load(
        [
            new ModLibraryItem("mod-1", "Alpha Mod", WorkshopId: "111", Author: "Alice", SourceReference: "steam://111")
        ]);

    subject.SelectMod("mod-1");

    Assert.True(subject.Inspector.HasSelection);
    Assert.Equal("Alpha Mod", subject.Inspector.DisplayName);
    Assert.Equal("111", subject.Inspector.WorkshopId);
    Assert.Equal("Alice", subject.Inspector.Author);
    Assert.Equal("steam://111", subject.Inspector.SourceReference);
  }

  [Fact]
  public void SelectedCollectionMarksMatchingModsAsMembers()
  {
    var subject = new ModLibraryViewModel();

    subject.Load(
        [
            new ModLibraryItem("mod-1", "Alpha Mod", CollectionNames: ["Core Collection", "Utility Collection"]),
            new ModLibraryItem("mod-2", "Beta Mod", CollectionNames: ["Other Collection"])
        ],
        [
            new CollectionSummary("collection-1", "Core Collection", 1),
            new CollectionSummary("collection-2", "Other Collection", 1)
        ]);

    subject.SelectCollection("collection-1");

    Assert.True(subject.Mods[0].IsInSelectedCollection);
    Assert.False(subject.Mods[1].IsInSelectedCollection);
  }

  [Fact]
  public void EmptyCollectionPromptAppearsForEmptySelection()
  {
    var subject = new CollectionDetailViewModel();

    subject.Load(
        [
            new CollectionSummary("collection-1", "Core Collection", 0),
            new CollectionSummary("collection-2", "Other Collection", 3)
        ]);

    subject.SelectCollection("collection-1");

    Assert.True(subject.IsEmpty);
    Assert.Equal("Import items into this Collection", subject.EmptyCollectionPrompt);
  }
}
