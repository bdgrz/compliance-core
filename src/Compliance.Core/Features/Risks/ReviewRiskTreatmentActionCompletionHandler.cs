using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Independently accepts or rejects a pending treatment action completion; HTTP-only. Admission freezes the exact
///     submission under review, the cited fulfilled evidence requests and the authored decision, replaces a live waiver
///     with an exact <see cref="SeparationOfDutiesWaiverProof" /> (the submitter or accountable member reviews only with
///     a proof that matches this action, submission, revision and reviewer) and is acknowledged with 202. The
///     decision itself is appended by <c>RiskActionCompletionReviewProcess</c> once every cited evidence request is held.
/// </summary>
public sealed class ReviewRiskTreatmentActionCompletionHandler(NativeOperationExecutor operations,
    IAggregateReader reader, RiskGovernanceReader governanceReader, ISeparationOfDutiesWaiverProofResolver waivers,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviewRiskTreatmentActionCompletion, OperationAcceptedResponse>
{
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<ReviewRiskTreatmentActionCompletion> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Refuse(RequestErrorKind.Forbidden, "Risk sign-off requires a personal HTTP invocation.");
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.RiskId == Uuid.Empty ||
            request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Refuse(RequestErrorKind.Validation,
                "A completion review requires a command id, an action id and a non-negative expected revision.");
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new RiskTreatmentAction(request.TenantId, request.ProgramId,
            request.RiskId, request.ActionId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, "ReviewRiskTreatmentActionCompletion",
            snapshot.Stream, actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;

        try
        {
            var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAction>(binding,
                () => new RiskTreatmentAction(request.TenantId, request.ProgramId, request.RiskId, request.ActionId,
                    request.CommandId),
                action => action.Acceptance,
                async (action, token) =>
                {
                    try
                    {
                        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
                            request.RiskId, token).ConfigureAwait(false);
                        if (!risk.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(risk.Error!);
                        var frozen = await FreezeAsync(request, action, provenance, actor, actorReference, now, token)
                            .ConfigureAwait(false);
                        if (!frozen.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                        (intent, response) = frozen.Value;
                        return Result<FirstResponseDescriptor>.Success(response!);
                    }
                    catch (ImmutableContentIntegrityException failure)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact treatment action source content cannot be verified.",
                            failure is ImmutableContentUnavailableException));
                    }
                },
                (RiskTreatmentAction action, ImmutableContentRef acknowledgment) =>
                    action.AcceptReview(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
            return await FindingContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact treatment action content cannot be verified.", failure is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        ReviewRiskTreatmentActionCompletion request, RiskTreatmentAction action, OperationProvenance provenance,
        RiskActor actor, ActorReference actorReference, DateTimeOffset now, CancellationToken ct)
    {
        var prepared = action.PrepareReview(request.ExpectedRevision);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);
        var tenant = request.TenantId;
        var candidate = Uuid.CreateVersion4();
        var pending = action.PendingSubmission!;
        var view = (await governanceReader.ComposeActionAsync(tenant, action, ct).ConfigureAwait(false))!;

        var assigned = await IsAssignedReviewerAsync(request, pending.SubmissionId, actor, ct).ConfigureAwait(false);
        var conflicts = pending.SubmitterMemberId == actor.MemberId || view.AccountableMemberId == actor.MemberId;
        if (!assigned && !(request.SeparationOfDutiesWaiverId is not null && conflicts))
            return Fail(RequestErrorKind.Forbidden, "Only the assigned reviewer may review this completion.");

        ImmutableContentRef? waiverReference = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
        {
            var scope = new SeparationOfDutiesWaiverProofScope(tenant, waiverId, actor.MemberId, actor.MemberId,
                SeparationOfDutiesActions.Review, SeparationOfDutiesRecordTypes.RiskTreatmentAction, request.ActionId,
                pending.SubmissionId, request.ExpectedRevision);
            var proof = await waivers.ResolveAsync(scope, now, ct).ConfigureAwait(false);
            if (!proof.IsSuccess)
                return Fail(RequestErrorKind.Forbidden,
                    "The separation-of-duties waiver is not active for this member and completion.");
            waiverReference = await FindingContent.WriteAsync(content, tenant, RiskContent.WaiverProofSchema,
                proof.Value!, ct).ConfigureAwait(false);
        }
        else if (conflicts)
            return Fail(RequestErrorKind.Forbidden,
                "The member who submitted or is accountable for the action cannot review its completion.");

        if (request.Outcome is not (RiskStates.Accept or RiskStates.Reject) ||
            RiskCommands.InvalidText(request.Rationale))
            return Fail(RequestErrorKind.Validation,
                "A completion review requires an outcome of accept or reject and a rationale of at most 4000 characters.");

        // Only an acceptance reads the cited evidence: each request must be fulfilled now, and its exact fulfilment is
        // pinned and held until the decision is recorded.
        var cited = new List<ReviewedEvidenceRequest>();
        var uses = new List<SourceUseRequirement>();
        if (request.Outcome == RiskStates.Accept)
            foreach (var id in view.Completions[^1].EvidenceRequestIds.OrderBy(id => RiskActionReviewEffects.EvidenceKey(
                         tenant, request.ProgramId, id), Kernel.StreamKeyOrder.Instance))
            {
                var evidence = await reader.HydrateAsync(new EvidenceRequest(tenant, request.ProgramId, id), ct)
                    .ConfigureAwait(false);
                if (evidence.FulfilledFact is not { } fulfilled)
                    return Fail(RequestErrorKind.Conflict,
                        "Completion cannot be accepted while a cited evidence request is not fulfilled.");
                var pinned = NativeFactReference.Of(evidence.Stream, fulfilled);
                cited.Add(new ReviewedEvidenceRequest(id, pinned));
                var facts = await FindingContent.WriteDeterministicAsync(content, tenant,
                    Uuid.CreateVersion5(candidate, id.ToString()), RiskContent.EvidenceFactsSchema,
                    new ReviewEvidenceFacts(id, true, fulfilled.ArtifactId, null), ct).ConfigureAwait(false);
                uses.Add(new SourceUseRequirement(evidence.Stream, pinned, (ulong)evidence.Revision,
                    SourceUsePurpose.ActionCompletionReview, facts));
            }

        var decision = new RiskActionReviewDecision(request.CommandId, request.ProgramId, request.RiskId,
            request.Outcome, request.Rationale.Trim(), actor.MemberId, actorReference, now,
            request.SeparationOfDutiesWaiverId, waiverReference);
        var decisionReference = await FindingContent.WriteDeterministicAsync(content, tenant, candidate,
            RiskContent.AuthoredReviewSchema, decision, ct).ConfigureAwait(false);
        var realm = tenant.ToString();
        var evidenceManifest = await ImmutableSelectionManifests.WriteAsync<ReviewedEvidenceRequest>(content, realm,
            Uuid.CreateVersion5(candidate, "completion-evidence"), RiskContent.ReviewedEvidenceSchema, cited,
            SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var useManifest = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, realm,
            Uuid.CreateVersion5(candidate, "source-uses"), SourceUseRequirement.Schema, uses,
            SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var intent = await FindingContent.WriteDeterministicAsync(content, tenant, candidate,
            RiskActionCompletionReviewIntent.Schema, new RiskActionCompletionReviewIntent(
                FindingContent.Header(provenance, now), request.ActionId, pending.SubmissionId, decisionReference,
                evidenceManifest, useManifest), ct).ConfigureAwait(false);
        var acknowledgment = await FindingContent.AcceptedResponseAsync(content, tenant, request.CommandId,
            RegisterFirmStaffHandler.StatusUrlTemplate, now, ct).ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, acknowledgment));
    }

    async ValueTask<bool> IsAssignedReviewerAsync(ReviewRiskTreatmentActionCompletion request, Uuid submissionId,
        RiskActor actor, CancellationToken ct)
    {
        var workItemId = WorkSource.RiskTreatmentActionReviewWorkItemId(request.ProgramId, request.RiskId, request.ActionId, submissionId);
        var assignment = await reader.HydrateAsync(new WorkItemAssignment(request.TenantId, request.ProgramId,
            workItemId), ct).ConfigureAwait(false);
        var assigned = assignment.AssigneeMemberId == actor.MemberId;
        if (!assigned && assignment.Revision == 0)
        {
            var legacy = await reader.HydrateAsync(new LegacyWorkAssignmentLedger(request.TenantId, request.ProgramId),
                ct).ConfigureAwait(false);
            assigned = legacy.Read(workItemId).AssigneeMemberId == actor.MemberId;
        }

        return assigned;
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message));

    static Result<OperationAcceptedResponse> Refuse(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
