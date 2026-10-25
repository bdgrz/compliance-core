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
///     on its own action stream: a work item is derived from its source workflow and that workflow alone owns
///     completion. A caller-supplied work identifier, a projection row or a fact of another kind or tenant is never
///     accepted.
/// </summary>
public sealed class FollowUpCompletionVerifier(ISourceFactResolver resolver, IImmutableContentStore content, IDomainEventReader events)
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
            if (reference.Stream is null || reference.EventId == Uuid.Empty || reference.AggregateVersion == 0 ||
                reference.Stream.Realm != tenantId.ToString() || reference.Stream.Area != CorrectiveAction.Area)
                return Reject("A follow-up completion must be a fact of a corrective action in this tenant.");
            try
            {
                DomainEventRecord? selected = null;
                try
                {
                    await foreach (var record in events.ReadAsync(reference.Stream, 0, ct).ConfigureAwait(false))
                    {
                        if (record.Stream != reference.Stream)
                            return Reject("The scoped completion history returned another native source stream.");
                        if (record.Event.Metadata.IsAudit || record.Event.Metadata.EventId != reference.EventId)
                            continue;
                        if (selected is not null || record.Event.Metadata.AggregateVersion != reference.AggregateVersion ||
                            record.Event is not CorrectiveActionCompletedV2)
                            return Reject("The named completion has no unique exact native source tuple.");
                        selected = record;
                    }
                }
                catch (InvalidOperationException)
                {
                    return Pending("The exact committed completion source is not readable yet.");
                }
                if (selected is null)
                    return Pending("The exact committed completion source is not visible yet.");
                var (proof, fact, locator) = await SourceFactProofs.BuildAsync<CorrectiveActionCompletedV2>(resolver,
                    content, tenantId.ToString(), reference, selected.ResourceOffset, ct).ConfigureAwait(false);
                if (fact.TenantId != tenantId || fact.ProgramId != programId ||
                    locator.Source != new CorrectiveAction(tenantId, programId, fact.FindingId, fact.ActionId).Stream)
                    return Reject("A follow-up completion must be a fact of a corrective action in this program.");
                completions.Add(new ComplianceIncidentFollowUpCompletionView(
                    WorkCandidate.IdFor(fact.ActionId, WorkSource.CorrectiveAction), proof));
            }
            catch (SourceFactUnavailableException exception)
            {
                return Pending($"The exact committed completion source is not visible yet: {exception.Message}");
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

    static Result<VerifiedIncidentResolution> Pending(string message) => Result<VerifiedIncidentResolution>.Failure(
        new RequestError(RequestErrorKind.Conflict, message, true));

    static Result<VerifiedIncidentResolution> Reject(string message) => Result<VerifiedIncidentResolution>.Failure(
        new RequestError(RequestErrorKind.Validation, message));
}
