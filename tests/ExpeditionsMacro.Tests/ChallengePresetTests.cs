using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengePresetTests
{
    [Fact]
    public void DraftPreset_CanBeSavedBeforeEveryModelIsChosen()
    {
        ChallengePreset preset = Draft();

        preset.Validate();
        Assert.Throws<InvalidDataException>(preset.ValidateReady);
    }

    [Fact]
    public void ReadyPreset_RequiresEveryMapToHaveCameraAndPlacementModels()
    {
        ChallengePreset preset = Draft() with
        {
            Maps = ChallengePreset.EmptyMapProfiles()
                .Select(profile => profile with
                {
                    CameraModelId = $"camera-{(int)profile.Map}",
                    PrestartPlacementModelId = $"placement-{(int)profile.Map}",
                })
                .ToArray(),
        };

        preset.ValidateReady();
    }

    [Fact]
    public void Preset_RequiresAnEnabledChallengeType()
    {
        ChallengePreset preset = Draft() with
        {
            RunTraitChallenge = false,
            RunStatChallenge = false,
            RunSpriteChallenge = false,
        };

        Assert.Throws<InvalidDataException>(preset.Validate);
    }

    [Fact]
    public void ExpeditionsFallback_RequiresAnExpeditionsPreset()
    {
        ChallengePreset preset = Draft() with { IdleBehavior = ChallengeIdleBehavior.RunExpeditions };

        Assert.Throws<InvalidDataException>(preset.Validate);
    }

    [Fact]
    public void DefeatRetries_DefaultToZeroAndRejectOutOfRangeValues()
    {
        Assert.Equal(0, Draft().DefeatRetries);
        Assert.Throws<InvalidDataException>(() => (Draft() with { DefeatRetries = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Draft() with { DefeatRetries = 21 }).Validate());
    }

    [Fact]
    public async Task Repository_RoundTripsAChallengeDraft()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ChallengePresetRepository repository = new(new AppPaths(root));
            ChallengePreset expected = Draft();

            await repository.SaveAsync(expected);

            ChallengePreset actual = Assert.Single(await repository.ListAsync());
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(Enum.GetValues<ChallengeMapId>(), actual.Maps.Select(profile => profile.Map));

            ChallengePreset loaded = Assert.IsType<ChallengePreset>(await repository.LoadAsync(expected.Id));
            Assert.Equal(expected.Id, loaded.Id);
            Assert.Equal(expected.DetectorPackId, loaded.DetectorPackId);
            Assert.Equal(
                expected.Maps.Select(profile => profile.Map),
                loaded.Maps.Select(profile => profile.Map));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static ChallengePreset Draft() => new()
    {
        Id = "challenge-test",
        Name = "Challenge test",
        Maps = ChallengePreset.EmptyMapProfiles(),
    };
}
