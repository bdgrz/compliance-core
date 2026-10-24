namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The compact set of administrator contributions rebuilt from an authoritative source cut while admission to
///     the guard was fenced. The first guard fact of a tenant references it as its source proofs.
/// </summary>
public sealed record GuardBaseline(IReadOnlyList<GuardBaselineContribution> Contributions, DateTimeOffset CapturedAt)
{
    /// <summary>Schema of the frozen baseline content.</summary>
    public const string Schema = "tenant-manager-baseline.v1";
}
