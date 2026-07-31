using System.Reflection;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Inspection;

public static class DetectorInspectionCatalog
{
    public static DetectorInspectionCatalogResult Create(
        IDetectorPack? detectorPack)
    {
        List<DetectorCatalogEntry> entries =
        [
            .. CompiledPackInspectionDefinitions.Create(
                detectorPack),
            .. GameModeInspectionDefinitions.Create(
                detectorPack),
            .. UtilityInspectionDefinitions.Create(),
        ];
        HashSet<Type> covered = entries
            .SelectMany(entry => entry.Owners)
            .ToHashSet();
        Type[] productionDetectors =
            typeof(DetectorInspectionCatalog)
                .Assembly
                .GetTypes()
                .Where(IsProductionDecisionOwner)
                .OrderBy(type => type.Namespace)
                .ThenBy(type => type.Name)
                .ToArray();
        foreach (Type detector in productionDetectors)
        {
            if (!covered.Contains(detector))
            {
                entries.Add(
                    DetectorInspectionDefinitionFactory
                        .Unavailable(detector));
            }
        }

        DetectorInspectionDefinition[] definitions =
            entries
                .Select(entry => entry.Definition)
                .DistinctBy(definition =>
                    definition.Id,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(definition =>
                    definition.Group)
                .ThenBy(definition =>
                    definition.DetailLevel ==
                    DetectorInspectionDetailLevel
                        .Unavailable)
                .ThenBy(definition =>
                    definition.Name)
                .ToArray();
        return new DetectorInspectionCatalogResult(
            definitions,
            productionDetectors.Length,
            definitions.Count(definition =>
                definition.CanEvaluate),
            definitions.Count(definition =>
                !definition.CanEvaluate));
    }

    private static bool IsProductionDecisionOwner(
        Type type)
    {
        if (type.Namespace is null ||
            !type.Namespace.StartsWith(
                "ExpeditionsMacro.Vision",
                StringComparison.Ordinal) ||
            type.Namespace.StartsWith(
                "ExpeditionsMacro.Vision.Inspection",
                StringComparison.Ordinal) ||
            type.Namespace.StartsWith(
                "ExpeditionsMacro.Vision.Infrastructure",
                StringComparison.Ordinal) ||
            type.Namespace.StartsWith(
                "ExpeditionsMacro.Vision.Diagnostics",
                StringComparison.Ordinal) ||
            type.IsNested ||
            type.IsGenericType)
        {
            return false;
        }
        if (type.Name.EndsWith(
                "Detector",
                StringComparison.Ordinal) ||
            type.Name.EndsWith(
                "Recognizer",
                StringComparison.Ordinal))
        {
            return true;
        }
        if (type.Name ==
            "CompiledDetectorPack")
        {
            return true;
        }
        return type
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Any(method =>
                method.GetParameters().Any(
                    parameter =>
                        ImageParameterType(
                            parameter
                                .ParameterType) ==
                        typeof(ImageFrame)));
    }

    private static Type ImageParameterType(
        Type type) =>
        type.IsByRef
            ? type.GetElementType() ?? type
            : type;
}
