namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Resolves the complete fixed operator policy path for an authenticated identity.</summary>
/// <remarks>
/// A qualified implementation binds every slot to its canonical exact owner for the supplied
/// credential and supported policy, including the complete identity, session, account, global
/// veto and operator path. Routing must be immutable for the selected policy or itself be
/// covered by the same coherent capture. Unknown or incomplete paths return null; projections
/// and caller-provided routes do not qualify. Qualification must remain valid through admission.
/// </remarks>
interface ITrustedAdmissionOperatorPathResolver
{
    bool IsQualified { get; }
    ValueTask<IReadOnlyList<AccessAdmissionStableRead.Participant>?> ResolveAsync(
        AccessAdmissionSemanticEvaluator.Credential actor, CancellationToken ct);
}
