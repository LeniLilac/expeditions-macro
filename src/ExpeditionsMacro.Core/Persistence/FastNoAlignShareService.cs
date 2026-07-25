using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class FastNoAlignShareService
{
    private readonly MacroPlanRepository _plans;
    private readonly PlacementModelRepository _placements;

    public FastNoAlignShareService(
        MacroPlanRepository plans,
        PlacementModelRepository placements)
    {
        _plans = plans;
        _placements = placements;
    }

    public async Task<string> ExportAsync(
        MacroPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        MacroPlan portable = plan with
        {
            Progress = [],
        };
        portable.Validate();

        List<PlacementModel> setups = [];
        HashSet<string> exportedIds =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (PlacementTarget target in
                 FastNoAlignShareBundle
                     .RequiredSetupTargets(portable))
        {
            PlacementModel setup =
                await LoadSetupAsync(
                    target,
                    cancellationToken)
                    .ConfigureAwait(false);
            if (exportedIds.Add(setup.Id))
            {
                setups.Add(setup);
            }
        }

        FastNoAlignShareBundle bundle = new()
        {
            Plan = portable,
            PlacementSetups = setups,
        };
        return FastNoAlignShareCodec.Encode(bundle);
    }

    public FastNoAlignShareBundle Read(
        string code) =>
        FastNoAlignShareCodec.Decode(code);

    private async Task<PlacementModel> LoadSetupAsync(
        PlacementTarget target,
        CancellationToken cancellationToken)
    {
        foreach (PlacementSetupRoute route in
                 PlacementSetupCatalog.CandidatesFor(target))
        {
            PlacementModel? setup =
                await _placements
                    .LoadAsync(
                        route.ModelId,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (setup is null)
            {
                continue;
            }
            setup.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                target);
            return setup;
        }

        throw new InvalidOperationException(
            $"Configure '{PlacementSetupCatalog.NameFor(target)}' in Placement Setup before exporting this plan.");
    }

    public async Task ImportAsync(
        FastNoAlignShareBundle bundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        bundle.Validate();

        foreach (PlacementModel setup in
                 bundle.PlacementSetups)
        {
            await _placements
                .SaveAsync(setup, cancellationToken)
                .ConfigureAwait(false);
        }
        await _plans
            .SaveAsync(
                bundle.Plan with
                {
                    Progress = [],
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
