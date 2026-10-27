using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.History;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>
///     Records the acting member's own verification and closure decision; HTTP-only. Admission seals action admission
///     for the finding generation, freezes the complete admitted action manifest and the exact source uses, and
///     replaces a live waiver with an exact <see cref="SeparationOfDutiesWaiverProof" />: a closer who owns the finding
///     or an admitted action needs a waiver whose proof matches this finding, revision and actor exactly. The closure
///     itself is appended by <c>FindingClosureProcess</c> once every action is held, so the first response is a 202.
/// </summary>
public sealed class CloseFindingHandler(NativeOperationExecutor operations, IDomainEventReader events,
    ISeparationOfDutiesWaiverProofResolver waivers, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CloseFinding, OperationAcceptedResponse>
{
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<CloseFinding> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Finding closure requires a personal HTTP invocation.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A closure requires a command id and a non-negative expected revision.");
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, "CloseFinding", snapshot.Stream, actorReference,
            (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var frozen = await FreezeAsync(request, owner, provenance, actor, actorReference, now, token)
                .ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
            (intent, response) = frozen.Value;
            return Result<FirstResponseDescriptor>.Success(response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            finding => finding.Acceptance,
            PrepareAsync,
            (FindingAggregate finding, ImmutableContentRef acknowledgment) =>
                finding.AcceptClosure(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        CloseFinding request, FindingAggregate finding, OperationProvenance provenance, OperationsActor actor,
        ActorReference actorReference, DateTimeOffset now, CancellationToken ct)
    {
        var prepared = finding.PrepareClosure(request.ExpectedRevision);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);
        var failure = FindingRules.ValidateClosure(request.VerificationRationale, request.ResolutionEvidence,
            request.Rationale);
        if (failure is not null)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(failure.ToRequestError());

        // The closer must be independent of the finding and of every admitted action owner, unless an exact waiver
        // proof covers this finding, revision and actor. The completers are verified when each action is held.
        var tenant = request.TenantId;
        var source = await FindingHistory.ReadAsync(events, finding, ct).ConfigureAwait(false);
        var owners = new List<Uuid> { finding.OwnerMemberId };
        foreach (var admitted in source.Admitted)
        {
            try
            {
                var admission = await FindingContent.ReadAsync<ChildAdmissionIntent>(content, tenant, admitted.Intent, ct)
                    .ConfigureAwait(false);
                owners.Add((await FindingContent.ReadAsync<AddCorrectiveAction>(content, tenant,
                    admission.CanonicalAuthoredContent, ct).ConfigureAwait(false)).OwnerMemberId);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Fail(RequestErrorKind.Conflict, $"The admitted action cannot be verified: {exception.Message}",
                    exception is ImmutableContentUnavailableException);
            }
        }

        ImmutableContentRef? waiverReference = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
        {
            var scope = new SeparationOfDutiesWaiverProofScope(tenant, waiverId, actor.MemberId, actor.MemberId,
                SeparationOfDutiesActions.Approve, SeparationOfDutiesRecordTypes.Finding, request.FindingId,
                request.FindingId, request.ExpectedRevision);
            var proof = await waivers.ResolveAsync(scope, now, ct).ConfigureAwait(false);
            if (!proof.IsSuccess)
                return Fail(RequestErrorKind.Forbidden,
                    "The separation-of-duties waiver is not active for this member and finding revision.");
            waiverReference = await FindingContent.WriteAsync(content, tenant, "separation-of-duties-waiver-proof.v1",
                proof.Value!, ct).ConfigureAwait(false);
        }
        else if (owners.Contains(actor.MemberId))
            return Fail(RequestErrorKind.Forbidden,
                "A finding or corrective action owner cannot verify and close their own remediation.");

        var decision = new ClosureDecision(request.CommandId, request.ProgramId, request.VerificationRationale.Trim(),
            FindingRules.AsUnresolved(request.ResolutionEvidence), request.Rationale.Trim(), actor.MemberId,
            actorReference, now, request.SeparationOfDutiesWaiverId);
        var decisionReference = await FindingContent.WriteAsync(content, tenant,
            FindingContent.DecisionSchema, decision, ct).ConfigureAwait(false);
        var children = source.Admitted.Select(fact => new AdmittedChild(fact.Operation.LogicalId,
            NativeFactReference.Of(finding.Stream, fact))).OrderBy(child => FindingClosureEffects.ActionKey(tenant,
            request.ProgramId, request.FindingId, child.ChildId), Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance).ToArray();
        var uses = children.Select(child => new SourceUseRequirement(
            new CorrectiveAction(tenant, request.ProgramId, request.FindingId, child.ChildId).Stream, child.Admission, 0,
            SourceUsePurpose.FindingClosure, source.Admitted.First(fact => fact.Operation.LogicalId == child.ChildId).Intent))
            .ToArray();
        var realm = tenant.ToString();
        var childManifest = await ImmutableSelectionManifests.WriteAsync<AdmittedChild>(content, realm,
            Uuid.CreateVersion4(), FindingContent.AdmittedChildSchema, children,
            SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var useManifest = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, realm,
            Uuid.CreateVersion4(), SourceUseRequirement.Schema, uses,
            SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var header = FindingContent.Header(provenance, now);
        var intent = await FindingContent.WriteAsync(content, tenant,
            ClosureIntent.Schema, new ClosureIntent(header, ClosureParentKind.Finding, request.FindingId,
                (ulong)finding.Generation, childManifest, decisionReference, useManifest, waiverReference), ct)
            .ConfigureAwait(false);
        var acknowledgment = await FindingContent.AcceptedResponseAsync(content, tenant, request.CommandId,
            RegisterFirmStaffHandler.StatusUrlTemplate, now, ct).ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, acknowledgment));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message,
        bool transient = false) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message, transient));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
