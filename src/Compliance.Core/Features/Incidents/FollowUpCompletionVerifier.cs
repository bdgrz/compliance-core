using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>
///     Turns the native facts named by a resolution into verified completions. The authoritative completion of a
///     follow-up work item is the committed <see cref="CorrectiveActionCompletedV2" /> fact of its corrective action
///     on the owning finding stream: a work item is derived from its source workflow and that workflow alone owns
///     completion. A caller-supplied work identifier, a projection row or a fact of another kind or tenant is never
///     accepted.
/// </summary>
public sealed class FollowUpCompletionVerifier(ISourceFactResolver resolver, IImmutableContentStore content)
{
    /// <summary>Resolves, reads and checks every named fact; any failure rejects the whole resolution.</summary>
    public async ValueTask<Result<VerifiedIncidentResolution>> VerifyAsync(Uuid tenantId, Uuid programId,
        ComplianceIncidentResolutionContent resolution, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (resolution.FollowUpCompletionFacts is null || resolution.FollowUpCompletionFacts.Any(static item => item is null))
            return Reject("Follow-up completion facts must be named by native fact references.");
        var completions = new List<ComplianceIncidentFollowUpCompletionView>();
        foreach (var reference in resolution.FollowUpCompletionFacts)
        {
            if (reference.Stream.Realm != tenantId.ToString() || reference.Stream.Area != FindingAggregate.Area)
                return Reject("A follow-up completion must be a fact of a finding in this tenant.");
            try
            {
                var (proof, fact, locator) = await SourceFactProofs.BuildAsync<CorrectiveActionCompletedV2>(resolver,
                    content, tenantId.ToString(), reference, ct).ConfigureAwait(false);
                if (fact.TenantId != tenantId || fact.ProgramId != programId ||
                    locator.Source != new FindingAggregate(tenantId, programId, fact.FindingId).Stream)
                    return Reject("A follow-up completion must be a fact of a finding in this program.");
                completions.Add(new ComplianceIncidentFollowUpCompletionView(
                    WorkCandidate.IdFor(fact.ActionId, WorkSource.CorrectiveAction), proof));
            }
            catch (SourceFactVerificationException exception)
            {
                return Reject($"A follow-up completion cannot be verified: {exception.Message}");
            }
        }

        if (completions.Select(static item => item.WorkItemId).Distinct().Count() != completions.Count)
            return Reject("Each follow-up work item can be completed by only one fact.");
        return Result<VerifiedIncidentResolution>.Success(new VerifiedIncidentResolution(resolution.Resolution,
            resolution.ResolvedAt, resolution.EvidenceArtifactIds, completions));
    }

    static Result<VerifiedIncidentResolution> Reject(string message) => Result<VerifiedIncidentResolution>.Failure(
        new RequestError(RequestErrorKind.Validation, message));
}
