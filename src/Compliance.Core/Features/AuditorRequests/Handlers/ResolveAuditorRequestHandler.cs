using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Projections;
using Bdgrz.Compliance.Features.AuditorRequests.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Accepts the resolution of a request at its owner revision. Admission seals response, comment and sample admission
///     for the request generation, requires the response selection to equal every admitted response, and freezes the
///     complete admitted child manifest, the authored decision and the exact source uses before the 202 acknowledgment.
///     A response that is already materialized without an accepted external disposition is refused here; everything else
///     is proven by <c>AuditorRequestResolutionProcess</c>, which appends the resolution once every response is held.
/// </summary>
public sealed class ResolveAuditorRequestHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IDomainEventReader events,
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
        var address = new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, address.Stream, actor,
            (ulong)request.ExpectedRevision, AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        AuditorRequestPreparedCut? preparedCut = null;
        AuditorRequestHistorySnapshot? history = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                history = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var frozen = await FreezeAsync(request, snapshot, history, provenance, actorMemberId, actor, now, prepareCt)
                    .ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                (intent, response) = frozen.Value;
                preparedCut = AuditorRequestPreparedCut.Capture(snapshot);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
            {
                if (preparedCut?.Matches(auditorRequest) != true)
                    return AuditorRequestPreparedCut.Changed();
                return auditorRequest.AcceptResolution(provenance, intent!, request.ResponseIds, acknowledgment, history!);
            }), context, ct)
            .ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        ResolveAuditorRequest request, AuditorRequest snapshot, AuditorRequestHistorySnapshot history, OperationProvenance provenance, Uuid actorMemberId,
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
        var prepared = snapshot.PrepareResolution(request.ExpectedRevision, request.ResponseIds, history);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);

        // A response that is already materialized without an accepted external disposition can never be held, so the
        // resolution is refused instead of accepted and canceled. Unmaterialized responses are proven by the process.
        var tenant = request.TenantId;
        foreach (var admitted in history.Responses)
        {
            var owner = await reader.HydrateAsync(new AuditorResponse(tenant, request.ProgramId, request.RequestId,
                admitted.Operation.LogicalId), ct).ConfigureAwait(false);
            if (owner.IsMaterialized && owner.Disposition?.Outcome != "accepted")
                return Fail(RequestErrorKind.Conflict,
                    "A request can be resolved only after every response has an accepted external disposition.");
        }

        var tenantRealm = tenant.ToString();
        var children = history.Responses.Select(fact => new AdmittedRequestChild(fact.Operation.LogicalId,
                AdmittedRequestChildKind.Response, NativeFactReference.Of(snapshot.Stream, fact)))
            .Concat(history.Comments.Select(fact => new AdmittedRequestChild(fact.Operation.LogicalId,
                AdmittedRequestChildKind.Comment, NativeFactReference.Of(snapshot.Stream, fact))))
            .Concat(history.Selections.Select(fact => new AdmittedRequestChild(fact.Operation.LogicalId,
                AdmittedRequestChildKind.SampleSelection, NativeFactReference.Of(snapshot.Stream, fact))))
            .OrderBy(child => (int)child.Kind).ThenBy(child => child.ChildId.ToString(), StringComparer.Ordinal)
            .ToArray();
        var uses = history.Responses
            .Select(fact => new SourceUseRequirement(new AuditorResponse(tenant, request.ProgramId, request.RequestId,
                    fact.Operation.LogicalId).Stream, NativeFactReference.Of(snapshot.Stream, fact), 0,
                SourceUsePurpose.RequestResolution, fact.Intent))
            .OrderBy(use => use.SourceOwner.Resource, Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance).ToArray();
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
