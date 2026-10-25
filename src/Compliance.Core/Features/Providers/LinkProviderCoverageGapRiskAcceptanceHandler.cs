using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Admits a link to an existing, still-active R1-07 acceptance as the root fact of the gap owner, then answers
///     202. The gap serializes the open check, the exact expected revision and the uniqueness of the (risk,
///     acceptance) pair; the active acceptance is proven at this admission instant. The immutable
///     <see cref="ProviderRiskAcceptance" /> is materialized afterwards by the independent child process and never
///     changes the gap. The command id is the logical identity and the link id.
/// </summary>
public sealed class LinkProviderCoverageGapRiskAcceptanceHandler(NativeOperationExecutor operations,
    IAggregateReader reader, AssuranceReferences references, ProviderReadConsistency providerConsistency,
    ISourceFactResolver resolver, IImmutableContentStore content, TimeProvider clock, RiskEvaluationReader evaluations)
    : IRequestHandler<LinkProviderCoverageGapRiskAcceptance, OperationAcceptedResponse>
{
    const string CommandKind = nameof(LinkProviderCoverageGapRiskAcceptance);

    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<LinkProviderCoverageGapRiskAcceptance> context, CancellationToken ct) =>
        ProviderGapFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(
        IRequestContext<LinkProviderCoverageGapRiskAcceptance> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ProviderId == Uuid.Empty || request.GapId == Uuid.Empty || request.RiskId == Uuid.Empty ||
            request.AcceptanceId == Uuid.Empty || request.ExpectedGapRevision < 1)
            return Fail(RequestErrorKind.Validation,
                "A provider coverage gap risk acceptance link requires program, risk, acceptance and gap identities and the expected gap revision.");
        var actor = ProviderActor.From(context);
        var now = clock.GetUtcNow();
        var provenance = Owner(request).Provenance(request.CommandId, CommandKind, actor,
            ProviderAssuranceGap.PairEpisode(request.RiskId, request.AcceptanceId),
            (ulong)request.ExpectedGapRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? intent = null;
        ProviderGapPreparedCut? preparedCut = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<ProviderAssuranceGap>(binding,
            () => Owner(request), owner => owner.Acceptance,
            async (owner, token) =>
            {
                preparedCut = ProviderGapPreparedCut.Capture(owner);
                var frozen = await FreezeAsync(request, owner, provenance, now, token).ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                intent = frozen.Value.Intent;
                return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
            },
            (ProviderAssuranceGap owner, ImmutableContentRef acknowledgment) =>
            {
                var guarded = owner.PrepareAdmitLink(request.ExpectedGapRevision, request.RiskId, request.AcceptanceId);
                if (!guarded.IsSuccess)
                    return guarded;
                return preparedCut is null || !preparedCut.Matches(owner) ? ProviderGapPreparedCut.Changed()
                    : owner.AdmitLink(provenance, request.ExpectedGapRevision, request.RiskId, request.AcceptanceId,
                        intent!, acknowledgment);
            }),
            context, ct).ConfigureAwait(false);
        return await ProviderAssuranceContent.ReadFirstResponseAsync<OperationAcceptedResponse>(content,
            request.TenantId, ProviderAssuranceContent.AcceptedSchema, accepted, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        LinkProviderCoverageGapRiskAcceptance request, ProviderAssuranceGap gap, OperationProvenance provenance,
        DateTimeOffset now, CancellationToken ct)
    {
        var current = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Failed(current.Error);
        var prepared = gap.PrepareAdmitLink(request.ExpectedGapRevision, request.RiskId, request.AcceptanceId);
        if (!prepared.IsSuccess)
            return Failed(prepared.Error!);
        if (gap.Recorded!.ProviderId != request.ProviderId)
            return Failed(new RequestError(RequestErrorKind.NotFound, "The provider coverage gap was not found."));
        ProviderCoverageGapContent recorded;
        try
        {
            recorded = await ProviderAssuranceContent.ReadAsync<ProviderCoverageGapContent>(content, request.TenantId,
                gap.Recorded.ContentContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Failed(new RequestError(RequestErrorKind.Conflict,
                $"The recorded provider coverage gap cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
        var provider = await providerConsistency.GetRevisionAsync(request.TenantId, request.ProviderId,
            gap.Recorded.ProviderRevision, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Failed(provider.Error);
        if (AssuranceReferences.ServiceError(provider.Value, recorded.ServiceId, request.ProgramId) is { } serviceError)
            return Failed(serviceError);
        var draft = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (!draft.IsCreated || draft.ProgramId != request.ProgramId)
            return Failed(new RequestError(RequestErrorKind.NotFound, "The risk was not found in this program."));
        RiskEvaluationReadSnapshot risk;
        try
        {
            risk = await evaluations.ReadAsync(request.TenantId, request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            ct.ThrowIfCancellationRequested();
            return Failed(RiskEvaluationReadFailure.Of(failure));
        }
        var acceptance = risk.ProgramId == request.ProgramId ? risk.FindAcceptance(request.AcceptanceId) : null;
        if (acceptance is null || risk.AcceptanceFact(request.AcceptanceId) is not { } fact ||
            acceptance.AcceptedAt > now || acceptance.ExpiresAt <= now)
            return Failed(new RequestError(RequestErrorKind.Conflict,
                "The risk acceptance is missing, expired, or not active."));
        SourceFactProof<RiskAccepted> proof;
        try
        {
            (proof, _, _) = await SourceFactProofs.BuildAsync<RiskAccepted>(resolver, content,
                ProviderAssuranceContent.Realm(request.TenantId), NativeFactReference.Of(risk.Stream, fact), ct)
                .ConfigureAwait(false);
        }
        catch (SourceFactVerificationException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Failed(new RequestError(RequestErrorKind.Conflict,
                $"The risk acceptance cannot be proven: {exception.Message}", exception is SourceFactUnavailableException));
        }
        var header = new DurableIntentHeader(request.CommandId, provenance.Owner, provenance.Owner, provenance.Owner,
            provenance.Actor, provenance.Episode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256,
            now);
        var intent = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
            ProviderRiskLinkIntent.Schema, new ProviderRiskLinkIntent(header, request.CommandId, request.ProviderId,
                request.GapId, (ulong)(gap.Revision + 1), request.ProgramId, request.RiskId, request.AcceptanceId,
                proof, acceptance.ExpiresAt, (ulong)gap.Recorded.ProviderRevision, recorded.ServiceId, now), ct)
            .ConfigureAwait(false);
        var response = await ProviderAssuranceContent.AcceptedAsync(content, request.TenantId, request.CommandId, now,
            ct).ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, response));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Failed(RequestError error) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(error);

    static ProviderAssuranceGap Owner(LinkProviderCoverageGapRiskAcceptance request) =>
        new(request.TenantId, request.ProviderId, request.GapId, request.CommandId,
            selectedPair: ProviderAssuranceGap.PairEpisode(request.RiskId, request.AcceptanceId));

    static Result<OperationAcceptedResponse> Fail(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
