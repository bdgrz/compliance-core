using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Accepts the resolution of a request at its owner revision. Admission seals response, comment and sample admission
///     for the request generation, requires the response selection to equal every admitted response, and freezes the
///     complete admitted child manifest, the authored decision and the exact source uses before the 202 acknowledgment.
///     A response that is already materialized without an accepted external disposition is refused here; everything else
///     is proven by <c>AuditorRequestResolutionProcess</c>, which appends the resolution once every response is held.
/// </summary>
public sealed class ResolveAuditorRequestHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ResolveAuditorRequest, OperationAcceptedResponse>
{
    const string CommandKind = "ResolveAuditorRequest";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<ResolveAuditorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden<OperationAcceptedResponse>(
                "Resolving an auditor request requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRevision < 0)
            return AuditorCommands.Validation<OperationAcceptedResponse>(
                "A resolution requires a command id and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var actor = AuditorCommands.Actor(actorMemberId, actorDisplay);
        var snapshot = await reader.HydrateAsync(new AuditorRequest(request.TenantId, request.ProgramId,
            request.RequestId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream, actor,
            (ulong)request.ExpectedRevision, AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var frozen = await FreezeAsync(request, snapshot, provenance, actorMemberId, actor, now, ct)
                .ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(frozen.Error!);
            (intent, response) = frozen.Value;
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId),
            auditorRequest => auditorRequest.Acceptance,
            auditorRequest => AuditorContent.Prepared(
                auditorRequest.PrepareResolution(request.ExpectedRevision, request.ResponseIds), response),
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
                auditorRequest.AcceptResolution(provenance, intent!, request.ResponseIds, acknowledgment)), context, ct)
            .ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        ResolveAuditorRequest request, AuditorRequest snapshot, OperationProvenance provenance, Uuid actorMemberId,
        ActorReference actor, DateTimeOffset now, CancellationToken ct)
    {
        var scoped = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, ct).ConfigureAwait(false);
        if (!scoped.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(scoped.Error!);
        if (request.ResponseIds is null || request.ResponseIds.Count == 0 ||
            request.ResponseIds.Any(static id => id == Uuid.Empty) ||
            request.ResponseIds.Distinct().Count() != request.ResponseIds.Count)
            return Fail(RequestErrorKind.Validation,
                "A resolution names a nonempty, duplicate-free selection of admitted responses.");
        var resolution = AuditorRequestRules.CleanText(request.Resolution);
        if (!AuditorRequestRules.Bounded(resolution, 1, AuditorRequestRules.MaximumRationaleLength))
            return Fail(RequestErrorKind.Validation, "Request resolution needs a bounded explanation.");
        var prepared = snapshot.PrepareResolution(request.ExpectedRevision, request.ResponseIds);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);

        // A response that is already materialized without an accepted external disposition can never be held, so the
        // resolution is refused instead of accepted and canceled. Unmaterialized responses are proven by the process.
        var tenant = request.TenantId;
        foreach (var admitted in snapshot.Responses)
        {
            var owner = await reader.HydrateAsync(new AuditorResponse(tenant, request.ProgramId, request.RequestId,
                admitted.Operation.LogicalId), ct).ConfigureAwait(false);
            if (owner.IsMaterialized && owner.Disposition?.Outcome != "accepted")
                return Fail(RequestErrorKind.Conflict,
                    "A request can be resolved only after every response has an accepted external disposition.");
        }

        var tenantRealm = tenant.ToString();
        var children = snapshot.Responses.Select(fact => new AdmittedRequestChild(fact.Operation.LogicalId,
                AdmittedRequestChildKind.Response, NativeFactReference.Of(snapshot.Stream, fact)))
            .Concat(snapshot.Comments.Select(fact => new AdmittedRequestChild(fact.Operation.LogicalId,
                AdmittedRequestChildKind.Comment, NativeFactReference.Of(snapshot.Stream, fact))))
            .Concat(snapshot.Selections.Select(fact => new AdmittedRequestChild(fact.Operation.LogicalId,
                AdmittedRequestChildKind.SampleSelection, NativeFactReference.Of(snapshot.Stream, fact))))
            .OrderBy(child => (int)child.Kind).ThenBy(child => child.ChildId.ToString(), StringComparer.Ordinal)
            .ToArray();
        var uses = snapshot.Responses
            .Select(fact => new SourceUseRequirement(new AuditorResponse(tenant, request.ProgramId, request.RequestId,
                    fact.Operation.LogicalId).Stream, NativeFactReference.Of(snapshot.Stream, fact), 0,
                SourceUsePurpose.RequestResolution, fact.Intent))
            .OrderBy(use => use.SourceOwner.Resource, Kernel.StreamKeyOrder.Instance).ToArray();
        var decision = new AuditorResolutionDecision(request.CommandId, request.ProgramId, resolution,
            AuditorRequestRules.Frozen(request.ResponseIds), actorMemberId, actor, now);
        var decisionReference = await AuditorContent.WriteAsync(content, tenant, AuditorContent.DecisionSchema, decision,
            ct).ConfigureAwait(false);
        var childManifest = await ImmutableSelectionManifests.WriteAsync<AdmittedRequestChild>(content, tenantRealm,
            Uuid.CreateVersion4(), AuditorContent.AdmittedChildSchema, children, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var useManifest = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, tenantRealm,
            Uuid.CreateVersion4(), SourceUseRequirement.Schema, uses, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var intent = await AuditorContent.WriteAsync(content, tenant, ClosureIntent.Schema, new ClosureIntent(
            AuditorContent.Header(provenance, now), ClosureParentKind.AuditorRequest, request.RequestId,
            (ulong)snapshot.Generation, childManifest, decisionReference, useManifest, null), ct).ConfigureAwait(false);
        var acknowledgment = await AuditorContent.AcceptedResponseAsync(content, tenant, request.CommandId, now, ct)
            .ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, acknowledgment));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message));
}
