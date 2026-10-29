using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Readers;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Materializes the immutable link from its admission. The admission is verified from the gap owner: the exact
///     fact, the frozen intent and the admitted revision. The gap is never changed, so a gap that closed after the
///     admission still materializes the admitted historical link, and the link keeps the admission revision and
///     instant. An exact retry replays the original acceptance.
/// </summary>
sealed class MaterializeProviderRiskAcceptanceLinkHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, ProviderGapReader history, ISourceFactResolver resolver)
    : IRequestHandler<MaterializeProviderRiskAcceptanceLink, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeProviderRiskAcceptanceLink> context, CancellationToken ct) =>
        ProviderGapFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ReceiverOutcome>> HandleCoreAsync(
        IRequestContext<MaterializeProviderRiskAcceptanceLink> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.OperationId == Uuid.Empty || request.LinkId != request.OperationId ||
            request.GapId == Uuid.Empty || !Uuid.TryParse(request.Source.Stream.Realm, out var tenantId) ||
            tenantId == Uuid.Empty || request.Target != ProviderAssuranceStreams.RiskAcceptance(tenantId, request.LinkId))
            return SourceUseReceiverSupport.Permanent("The materialization names another link, gap or tenant.");
        var gap = await history.ReadFromSourceAsync(request.Source, request.GapId, request.LinkId, ct).ConfigureAwait(false);
        if (gap.AdmissionOf(request.LinkId) is not { } admitted)
            return gap.Facts.Any(value => value.Metadata.EventId == request.Source.EventId)
                ? SourceUseReceiverSupport.Permanent("The source admits another link operation.")
                : SourceUseReceiverSupport.Transient("The admitted link is not visible yet.");
        if (request.Source.Stream != gap.Stream || admitted.Metadata.EventId != request.Source.EventId ||
            admitted.Metadata.AggregateVersion != request.Source.AggregateVersion || admitted.Intent != request.Intent ||
            admitted.Revision != request.Generation)
            return SourceUseReceiverSupport.Permanent("The source is not the admission of this link.");
        var operation = admitted.Operation;
        var header = new DurableIntentHeader(operation.RootOperationId, gap.Stream, gap.Stream, gap.Stream,
            operation.Actor, operation.Episode, operation.OriginalExpectedRevision, operation.AuthoredIntentSha256,
            operation.AcceptedAt);
        // Preserve this receiver's actual existing child binding, including its original optional FirstOwner semantics.
        var provenance = new ProviderRiskAcceptance(tenantId, request.LinkId).Provenance(request.CommandId,
            nameof(MaterializeProviderRiskAcceptanceLink), request.Source.EventId, header, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<ProviderRiskAcceptance, Prepared>(reader,
            () => new ProviderRiskAcceptance(tenantId, request.LinkId, request.CommandId), owner => Decide(owner, binding),
            (_, token) => PrepareAsync(tenantId, gap, admitted, request, header, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var prepared = preparation.Value.Value!;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var owner = await reader.HydrateAsync(new ProviderRiskAcceptance(tenantId, request.LinkId,
                    request.CommandId), ct).ConfigureAwait(false);
                if (Decide(owner, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new ProviderRiskAcceptance(tenantId, request.LinkId,
                    request.CommandId), fresh =>
                {
                    if (Decide(fresh, binding) is { } decided)
                        return AggregateOutcome.Discard(Result<Decision>.Success(new Decision(decided)));
                    var raised = fresh.Materialize(provenance, request.GapId, gap.ProviderId, admitted.Revision,
                        prepared.Acceptance, request.Intent, prepared.Result);
                    return raised.IsSuccess
                        ? AggregateOutcome.Commit(Result<Decision>.Success(new Decision(
                            SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                fresh.Acceptance.SelectedFact!)))))
                        : AggregateOutcome.Discard(Result<Decision>.Failure(raised.Error!));
                }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The link changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Result<Prepared>> PrepareAsync(Uuid tenantId, ProviderGapSnapshot gap,
        ProviderCoverageGapRiskAcceptanceAdmitted admitted, MaterializeProviderRiskAcceptanceLink request,
        DurableIntentHeader header, CancellationToken ct)
    {
        ProviderRiskLinkIntent intent;
        try
        {
            intent = await ProviderAssuranceContent.ReadAsync<ProviderRiskLinkIntent>(content, tenantId, request.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<Prepared>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen link cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
        if (intent.Header != header || intent.LinkId != request.LinkId || intent.GapId != request.GapId ||
            intent.ProviderId != gap.ProviderId || intent.GapAdmissionRevision != (ulong)admitted.Revision ||
            intent.ProviderRevision != (ulong)gap.Recorded!.ProviderRevision || intent.LinkedAt != header.AcceptedAt ||
            intent.ProgramId == Uuid.Empty || intent.RiskId == Uuid.Empty || intent.AcceptanceId == Uuid.Empty ||
            header.OriginalExpectedRevision is not { } original || original > long.MaxValue ||
            ProviderAssuranceGap.PairEpisode(intent.RiskId, intent.AcceptanceId) != header.Episode ||
            AuthoredIntentDigest.Compute(new LinkProviderCoverageGapRiskAcceptance(request.LinkId, tenantId,
                intent.ProgramId, gap.ProviderId, request.GapId, (long)original, intent.RiskId, intent.AcceptanceId)) !=
                header.AuthoredIntentSha256 || intent.AcceptanceProof is null ||
            intent.AcceptanceProof.Source != new RiskEvaluation(tenantId, intent.RiskId).Stream)
            return Result<Prepared>.Failure(SourceUseReceiverSupport.Permanent(
                "The frozen link differs from its exact native admission and original authored terms.").Error!);
        var recorded = await ProviderAssuranceContent.ReadAsync<ProviderCoverageGapContent>(content, tenantId,
            gap.Recorded.ContentContent, ct).ConfigureAwait(false);
        if (recorded.ServiceId != intent.ServiceId)
            return Result<Prepared>.Failure(SourceUseReceiverSupport.Permanent(
                "The frozen link names another exact recorded provider service.").Error!);
        VerifiedSourceProof<RiskAccepted> proof;
        try
        {
            proof = await resolver.VerifyAsync(intent.AcceptanceProof, ct).ConfigureAwait(false);
        }
        catch (SourceFactVerificationException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<Prepared>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The captured risk acceptance proof cannot be verified: " + exception.Message,
                exception is SourceFactUnavailableException));
        }
        var fact = proof.Facts;
        if (fact.TenantId != tenantId || fact.ProgramId != intent.ProgramId || fact.RiskId != intent.RiskId || fact.Header is null ||
            fact.Header.LatestAcceptanceId != intent.AcceptanceId)
            return Result<Prepared>.Failure(SourceUseReceiverSupport.Permanent(
                "The captured source is another exact risk acceptance.").Error!);
        var accepted = await RiskEvaluationContent.ReadAcceptanceAsync(content, fact, ct).ConfigureAwait(false);
        if (accepted.AcceptanceId != intent.AcceptanceId || accepted.ExpiresAt != intent.AcceptanceExpiresAt ||
            accepted.AcceptedAt > intent.LinkedAt || intent.AcceptanceExpiresAt <= intent.LinkedAt)
            return Result<Prepared>.Failure(SourceUseReceiverSupport.Permanent(
                "The captured acceptance was not active at the original link admission instant.").Error!);
        var view = new ProviderCoverageGapRiskAcceptanceView(intent.ProgramId, intent.RiskId, intent.AcceptanceId,
            intent.AcceptanceExpiresAt, header.OriginalActor, intent.LinkedAt)
        { Revision = admitted.Revision };
        var acceptance = await ProviderAssuranceContent.WriteAsync(content, tenantId, ProviderAssuranceContent.LinkSchema,
            view, ct, Uuid.CreateVersion5(request.CommandId, ProviderAssuranceContent.LinkSchema)).ConfigureAwait(false);
        var result = await ProviderAssuranceContent.WriteAsync(content, tenantId, ProviderAssuranceContent.GapRegistrationSchema,
            new ProviderCoverageGapRegistration(request.GapId, admitted.Revision), ct,
            Uuid.CreateVersion5(request.CommandId, ProviderAssuranceContent.GapRegistrationSchema)).ConfigureAwait(false);
        return Result<Prepared>.Success(new Prepared(acceptance, result));
    }

    static Result<ReceiverOutcome>? Decide(ProviderRiskAcceptance owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        return owner.Linked is { } linked
            ? SourceUseReceiverSupport.Superseded("The link is already materialized.",
                NativeFactReference.Of(owner.Stream, linked))
            : null;
    }

    sealed record Prepared(ImmutableContentRef Acceptance, ImmutableContentRef Result);

    sealed record Decision(Result<ReceiverOutcome> Outcome);
}
