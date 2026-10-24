using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Records the acting member's own verification and closure decision; HTTP-only. Admission seals action admission
///     for the finding generation, freezes the complete admitted action manifest and the exact source uses, and
///     replaces a live waiver with an exact <see cref="SeparationOfDutiesWaiverProof" />: a closer who owns the finding
///     or an admitted action needs a waiver whose proof matches this finding, revision and actor exactly. The closure
///     itself is appended by <c>FindingClosureProcess</c> once every action is held, so the first response is a 202.
/// </summary>
public sealed class CloseFindingHandler(NativeOperationExecutor operations, IAggregateReader reader,
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
        var snapshot = await reader.HydrateAsync(new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "CloseFinding", snapshot.Stream, actorReference,
            (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var frozen = await FreezeAsync(request, snapshot, provenance, actor, actorReference, now, ct)
                .ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(frozen.Error!);
            (intent, response) = frozen.Value;
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            finding => finding.Acceptance,
            finding => FindingCommands.Prepared(finding.PrepareClosure(request.ExpectedRevision), response),
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
        var owners = new List<Uuid> { finding.OwnerMemberId };
        foreach (var admitted in finding.Admitted)
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
                return Fail(RequestErrorKind.Conflict, $"The admitted action cannot be verified yet: {exception.Message}",
                    true);
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
        var decisionReference = await FindingContent.WriteDeterministicAsync(content, tenant, request.CommandId,
            FindingContent.DecisionSchema, decision, ct).ConfigureAwait(false);
        var children = finding.Admitted.Select(fact => new AdmittedChild(fact.Operation.LogicalId,
            NativeFactReference.Of(finding.Stream, fact))).OrderBy(child => FindingClosureEffects.ActionKey(tenant,
            request.ProgramId, request.FindingId, child.ChildId), Kernel.StreamKeyOrder.Instance).ToArray();
        var uses = children.Select(child => new SourceUseRequirement(
            new CorrectiveAction(tenant, request.ProgramId, request.FindingId, child.ChildId).Stream, child.Admission, 0,
            SourceUsePurpose.FindingClosure, finding.Admitted.First(fact => fact.Operation.LogicalId == child.ChildId).Intent))
            .ToArray();
        var realm = tenant.ToString();
        var childManifest = await ImmutableSelectionManifests.WriteAsync<AdmittedChild>(content, realm,
            Uuid.CreateVersion5(request.CommandId, "admitted-children"), FindingContent.AdmittedChildSchema, children,
            SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var useManifest = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, realm,
            Uuid.CreateVersion5(request.CommandId, "source-uses"), SourceUseRequirement.Schema, uses,
            SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var header = FindingContent.Header(provenance, now);
        var intent = await FindingContent.WriteDeterministicAsync(content, tenant, request.CommandId,
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
