using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Camera;

internal readonly record struct FullTurnRefinement(int BestOffset, ImageFrame Frame, double Score);

internal readonly record struct VerifiedFullTurnCandidate(int Step, double RefinedScore);

internal readonly record struct FineYawReference(int Offset, ImageFrame Thumbnail);

internal readonly record struct FineYawMatch(int Offset, double Score);

internal readonly record struct AlignmentObservation(double DirectScore, FineYawMatch FineMatch);

internal readonly record struct AlignmentAttemptPlan(CameraYawDirection ScanDirection, int ScanPhasePixels);

internal readonly record struct AtlasMatch(int Index, double Score);
