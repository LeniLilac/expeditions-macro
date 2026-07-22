using System.ComponentModel;
using System.Runtime.CompilerServices;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed record CatalogOption(string Id, string Name);

public sealed class ChallengeMapRow : INotifyPropertyChanged
{
    private string _cameraModelId = string.Empty;
    private string _prestartPlacementModelId = string.Empty;
    private string _delayedPlacementModelId = string.Empty;
    private int _delayedPlacementSeconds = 30;
    private int _teamSlot;

    public ChallengeMapRow(ChallengeMapId map)
    {
        Map = map;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChallengeMapId Map { get; }

    public string MapName => Map switch
    {
        ChallengeMapId.SchoolGrounds => "School Grounds",
        ChallengeMapId.FlowerForest => "Flower Forest",
        ChallengeMapId.RoseKingdom => "Rose Kingdom",
        ChallengeMapId.FairyKingForest => "Fairy King Forest",
        ChallengeMapId.KingsTomb => "King's Tomb",
        _ => "Unknown map",
    };

    public string CameraModelId
    {
        get => _cameraModelId;
        set => SetField(ref _cameraModelId, value);
    }

    public string PrestartPlacementModelId
    {
        get => _prestartPlacementModelId;
        set => SetField(ref _prestartPlacementModelId, value);
    }

    public string DelayedPlacementModelId
    {
        get => _delayedPlacementModelId;
        set => SetField(ref _delayedPlacementModelId, value);
    }

    public int DelayedPlacementSeconds
    {
        get => _delayedPlacementSeconds;
        set => SetField(ref _delayedPlacementSeconds, value);
    }

    public int TeamSlot
    {
        get => _teamSlot;
        set => SetField(ref _teamSlot, value);
    }

    public ChallengeMapProfile ToProfile() => new()
    {
        Map = Map,
        CameraModelId = CameraModelId,
        PrestartPlacementModelId = PrestartPlacementModelId,
        DelayedPlacementModelId = DelayedPlacementModelId,
        DelayedPlacementSeconds = DelayedPlacementSeconds,
        TeamSlot = TeamSlot,
    };

    public void Apply(ChallengeMapProfile profile)
    {
        if (profile.Map != Map) throw new ArgumentException("Map profile does not match this row.", nameof(profile));
        CameraModelId = profile.CameraModelId;
        PrestartPlacementModelId = profile.PrestartPlacementModelId;
        DelayedPlacementModelId = profile.DelayedPlacementModelId;
        DelayedPlacementSeconds = profile.DelayedPlacementSeconds;
        TeamSlot = profile.TeamSlot;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
