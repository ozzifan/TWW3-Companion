using System.Collections.ObjectModel;

namespace Tww3Companion.Desktop.ViewModels;

public sealed record ModLibraryItem(
    string ModId,
    string DisplayName,
    string? WorkshopId = null,
    string? Author = null,
    string? SourceReference = null,
    DateTimeOffset? DateAdded = null,
    IReadOnlyList<string>? CollectionNames = null);

public sealed record CollectionSummary(
    string CollectionId,
    string DisplayName,
    int MemberCount);

public sealed class ModListItemViewModel : ViewModelBase
{
  private bool isInSelectedCollection;

  public ModListItemViewModel(ModLibraryItem mod)
  {
    Mod = mod;
    DisplayName = mod.DisplayName;
    WorkshopId = mod.WorkshopId ?? string.Empty;
    Author = mod.Author ?? string.Empty;
    SourceReference = mod.SourceReference ?? string.Empty;
    DateAdded = mod.DateAdded;
    CollectionNames = mod.CollectionNames ?? [];
  }

  public ModLibraryItem Mod { get; }

  public string DisplayName { get; }

  public string WorkshopId { get; }

  public string Author { get; }

  public string SourceReference { get; }

  public DateTimeOffset? DateAdded { get; }

  public IReadOnlyList<string> CollectionNames { get; }

  public bool IsInSelectedCollection
  {
    get => isInSelectedCollection;
    private set
    {
      if (isInSelectedCollection == value)
      {
        return;
      }

      isInSelectedCollection = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasSelectedCollectionMarker));
    }
  }

  public bool HasSelectedCollectionMarker => IsInSelectedCollection;

  internal void SetSelectedCollectionMembership(bool isMember) => IsInSelectedCollection = isMember;
}

public sealed class ModDetailInspectorViewModel : ViewModelBase
{
  private ModListItemViewModel? selectedMod;

  public ModListItemViewModel? SelectedMod
  {
    get => selectedMod;
    private set
    {
      if (ReferenceEquals(selectedMod, value))
      {
        return;
      }

      selectedMod = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasSelection));
      OnPropertyChanged(nameof(DisplayName));
      OnPropertyChanged(nameof(WorkshopId));
      OnPropertyChanged(nameof(Author));
      OnPropertyChanged(nameof(SourceReference));
      OnPropertyChanged(nameof(DateAdded));
    }
  }

  public bool HasSelection => SelectedMod is not null;

  public string DisplayName => SelectedMod?.DisplayName ?? "No mod selected";

  public string WorkshopId => SelectedMod?.WorkshopId ?? string.Empty;

  public string Author => SelectedMod?.Author ?? string.Empty;

  public string SourceReference => SelectedMod?.SourceReference ?? string.Empty;

  public DateTimeOffset? DateAdded => SelectedMod?.DateAdded;

  public void Select(ModListItemViewModel? mod) => SelectedMod = mod;

  public void Clear() => SelectedMod = null;
}

public sealed class ModLibraryViewModel : ViewModelBase
{
  private readonly ObservableCollection<ModListItemViewModel> mods = [];
  private readonly Dictionary<string, ModListItemViewModel> modsById = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, CollectionSummary> collectionsById = new(StringComparer.OrdinalIgnoreCase);
  private string? selectedCollectionId;

  public ModLibraryViewModel()
  {
    Inspector = new ModDetailInspectorViewModel();
  }

  public IReadOnlyList<ModListItemViewModel> Mods => mods;

  public IReadOnlyList<CollectionSummary> Collections => collectionsById.Values.ToList();

  public ModDetailInspectorViewModel Inspector { get; }

  public CollectionSummary? SelectedCollection =>
      selectedCollectionId is null || !collectionsById.TryGetValue(selectedCollectionId, out var collection)
          ? null
          : collection;

  public bool HasMods => mods.Count > 0;

  public void Load(IReadOnlyList<ModLibraryItem> items, IReadOnlyList<CollectionSummary>? collections = null)
  {
    ArgumentNullException.ThrowIfNull(items);

    mods.Clear();
    modsById.Clear();

    foreach (var item in items)
    {
      var mod = new ModListItemViewModel(item);
      mods.Add(mod);
      modsById[item.ModId] = mod;
    }

    collectionsById.Clear();
    if (collections is not null)
    {
      foreach (var collection in collections)
      {
        collectionsById[collection.CollectionId] = collection;
      }
    }

    ApplyCollectionMembership();
    if (Inspector.SelectedMod is not null && !modsById.ContainsKey(Inspector.SelectedMod.Mod.ModId))
    {
      Inspector.Clear();
    }

    OnPropertyChanged(nameof(Mods));
    OnPropertyChanged(nameof(Collections));
    OnPropertyChanged(nameof(SelectedCollection));
    OnPropertyChanged(nameof(HasMods));
  }

  public void SelectCollection(string? collectionId)
  {
    selectedCollectionId = string.IsNullOrWhiteSpace(collectionId) ? null : collectionId;
    ApplyCollectionMembership();
    OnPropertyChanged(nameof(SelectedCollection));
  }

  public void SelectMod(string modId)
  {
    if (!modsById.TryGetValue(modId, out var mod))
    {
      Inspector.Clear();
      return;
    }

    Inspector.Select(mod);
  }

  private void ApplyCollectionMembership()
  {
    var selectedCollection = SelectedCollection;
    foreach (var mod in mods)
    {
      var isMember = selectedCollection is not null && mod.Mod.CollectionNames?.Contains(selectedCollection.DisplayName, StringComparer.OrdinalIgnoreCase) == true;
      mod.SetSelectedCollectionMembership(isMember);
    }
  }
}

public sealed class CollectionDetailViewModel : ViewModelBase
{
  private readonly ObservableCollection<CollectionSummary> collections = [];
  private CollectionSummary? selectedCollection;

  public IReadOnlyList<CollectionSummary> Collections => collections;

  public CollectionSummary? SelectedCollection
  {
    get => selectedCollection;
    private set
    {
      if (ReferenceEquals(selectedCollection, value))
      {
        return;
      }

      selectedCollection = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsEmpty));
      OnPropertyChanged(nameof(EmptyCollectionPrompt));
      OnPropertyChanged(nameof(HasSelection));
    }
  }

  public bool HasSelection => SelectedCollection is not null;

  public bool IsEmpty => SelectedCollection is null || SelectedCollection.MemberCount == 0;

  public string EmptyCollectionPrompt => SelectedCollection is null
      ? "Select a collection to see its members."
      : SelectedCollection.MemberCount == 0
          ? "Import items into this Collection"
          : string.Empty;

  public void Load(IReadOnlyList<CollectionSummary> items)
  {
    ArgumentNullException.ThrowIfNull(items);
    collections.Clear();
    foreach (var item in items)
    {
      collections.Add(item);
    }

    if (SelectedCollection is not null && !collections.Any(collection => collection.CollectionId == SelectedCollection.CollectionId))
    {
      SelectedCollection = null;
    }

    OnPropertyChanged(nameof(Collections));
  }

  public void SelectCollection(string collectionId)
  {
    SelectedCollection = collections.FirstOrDefault(collection => collection.CollectionId == collectionId);
  }
}
