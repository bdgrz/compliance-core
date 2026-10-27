using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.SourceFacts;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Admits a historical preview on the service-epoch owner of the client. Admission freezes the complete service
///     history of the exact epoch, the exact draft rule version and the committed frontier of every source it read,
///     then acknowledges with a fixed 202. The preview is computed later from that snapshot only, so an exact retry
///     returns the original acknowledgment even after services or rules change.
/// </summary>
public sealed class EvaluateClientIndependenceHandler(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock,
    IndependenceRuleCatalogReader ruleSources, IDomainEventReader events, ISourceFactResolver sources)
    : IRequestHandler<EvaluateClientIndependence, OperationAcceptedResponse>
{
    const string CommandKind = "EvaluateClientIndependence";
    const string InitialEpisode = "initial";

    /// <summary>The status location of an evaluation; the status route is reserved until operation status exists.</summary>
    public const string StatusUrlTemplate = "/api/v1/tenants/{tenant_id}/operations/{operation_id}";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<EvaluateClientIndependence> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.EvaluationId == Uuid.Empty ||
            request.ExaminationPeriodStart == DateOnly.MinValue || request.RuleSetVersion <= 0)
            return IndependenceContent.Failure<OperationAcceptedResponse>(RequestErrorKind.Validation,
                "An evaluation requires an owning client, immutable identity, rule version and examination period.");
        if (request.ExpectedServiceEpoch < 0)
            return IndependenceContent.Failure<OperationAcceptedResponse>(RequestErrorKind.Validation,
                "The expected service epoch cannot be negative.");

        var now = clock.GetUtcNow();
        var actor = AccessGrantActor.From(context, request.TenantId);
        var realm = IndependenceContent.Realm(request.TenantId);
        var stream = new ClientServiceIndependence(request.TenantId).Stream;
        var provenance = new OperationProvenance(request.EvaluationId, request.EvaluationId, null, CommandKind, actor,
            stream, InitialEpisode, (ulong)request.ExpectedServiceEpoch, AuthoredIntentDigest.Compute(request), now,
            stream, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        ulong intentEpoch = 0;
        (ulong Version, ulong Position, string Digest)? preparedCut = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(ClientServiceIndependence owner, CancellationToken token)
        {
            var frozen = await FreezeAsync(request, owner, provenance, now, token).ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
            intent = frozen.Value.Intent;
            intentEpoch = owner.Epoch;
            preparedCut = (owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
            return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ClientServiceIndependence>(binding,
            () => new ClientServiceIndependence(request.TenantId, request.EvaluationId), owner => owner.Acceptance,
            PrepareAsync,
            (ClientServiceIndependence owner, ImmutableContentRef acknowledgment) =>
                preparedCut == (owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest)
                    ? owner.AdmitEvaluation(provenance, intent!, intentEpoch, acknowledgment)
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The prepared client preview prefix changed before admission.", true))), context, ct)
            .ConfigureAwait(false);
        return await IndependenceContent.ReadAcknowledgmentAsync(content, realm, outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        EvaluateClientIndependence request, ClientServiceIndependence snapshot, OperationProvenance provenance,
        DateTimeOffset now, CancellationToken ct)
    {
        var prepared = snapshot.PrepareEvaluation(request.EvaluationId, request.ExpectedServiceEpoch);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);
        IndependenceRuleCatalogReadSnapshot catalog;
        try
        { catalog = await ruleSources.ReadAsync(ct).ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(IndependenceCatalogContent.Failure(error));
        }
        if (catalog.Current is not { } version || version.Version != request.RuleSetVersion)
            return IndependenceContent.Failure<(ImmutableContentRef, FirstResponseDescriptor)>(
                RequestErrorKind.Conflict,
                "The draft rule source changed. Reload its exact version before evaluating. Retained previews never grant acceptance.",
                true);
        if (version.IsRatified || !IndependenceRecordValidation.ValidRules(version.Content))
            return IndependenceContent.Failure<(ImmutableContentRef, FirstResponseDescriptor)>(
                RequestErrorKind.Validation,
                "An evaluation requires an owning client, immutable identity, examination period and member attribution.");
        version = IndependenceRecordValidation.Freeze(version);

        var realm = IndependenceContent.Realm(request.TenantId);
        IReadOnlyList<NonattestServiceView> services;
        try
        {
            services = await ClientServiceHistory.ReadAsync(events, content, snapshot, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(IndependenceCatalogContent.Failure(error));
        }

        // The preview is a pure function of the frozen inputs, so an unevaluable snapshot is refused here, before
        // anything is accepted; the receiver can then only fail for infrastructure reasons.
        var preview = ClientIndependencePreviewEvaluator.Evaluate(request.TenantId, request.EvaluationId, version,
            request.ExaminationPeriodStart, services, provenance.Actor, now);
        if (!preview.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(preview.Error!);

        var cuts = new[]
        {
            await IndependenceSourceCut.CaptureAsync(events, sources, snapshot, clock, ct).ConfigureAwait(false),
            await IndependenceSourceCut.CaptureAsync(events, sources, catalog.Owner, clock, ct).ConfigureAwait(false),
        };
        var header = new DurableIntentHeader(request.EvaluationId, snapshot.Stream, snapshot.Stream, snapshot.Stream,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now);
        var completeServices = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            ClientIndependencePreviewIntent.ServiceItemSchema, services, SelectionOrder.Authored, ct)
            .ConfigureAwait(false);
        var ruleContent = await IndependenceContent.WriteAsync(content, realm, ClientIndependencePreviewIntent.RuleSchema,
            version, ct).ConfigureAwait(false);
        var practice = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            ClientIndependencePreviewIntent.PracticeItemSchema, Array.Empty<SourceFactLocator>(),
            SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var sourceCuts = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            ClientIndependencePreviewIntent.CutItemSchema, cuts, SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var intent = await IndependenceContent.WriteAsync(content, realm, ClientIndependencePreviewIntent.Schema,
            new ClientIndependencePreviewIntent(header, request.EvaluationId, request.TenantId, snapshot.Epoch,
                request.RuleSetVersion, request.ExaminationPeriodStart, completeServices, ruleContent, practice,
                sourceCuts, IndependenceSourceDigest.RuleContent(version.Content)), ct).ConfigureAwait(false);
        var acknowledgment = await IndependenceContent.WriteAsync(content, realm, "operation-accepted-response.v1",
            new OperationAcceptedResponse(request.EvaluationId, StatusUrlTemplate
                .Replace("{tenant_id}", request.TenantId.ToString(), StringComparison.Ordinal)
                .Replace("{operation_id}", request.EvaluationId.ToString(), StringComparison.Ordinal), now), ct)
            .ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent,
            new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
                "operation-accepted-response.v1", new StandardResponseBody(acknowledgment))));
    }

}
