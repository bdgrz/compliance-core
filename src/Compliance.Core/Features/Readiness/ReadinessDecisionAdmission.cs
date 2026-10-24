using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The shared admission of a management decision. It seals the latest adoption on the program readiness owner at the
///     expected revision, freezes the complete gap set with the plan each gap had, the acknowledged gaps, the exact
///     gap plan uses in canonical stream-key order and an exact waiver proof (never a live waiver), and acknowledges
///     with the frozen 202. Business rules are evaluated on current state here and again by the completing receiver at
///     the decision instant over the held plans; a plan that changes before it is held fences the decision.
/// </summary>
sealed class ReadinessDecisionAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    ReadinessReader readiness, ISeparationOfDutiesWaiverProofResolver waivers, ISourceFactResolver facts,
    IImmutableContentStore content, TimeProvider clock)
{
    /// <summary>Admits one decision, or replays the original acknowledgment of an exact retry.</summary>
    public async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync<TRequest>(ReadinessDecisionKind kind,
        IRequestContext<TRequest> context, TRequest request, Uuid commandId, Uuid tenantId, Uuid programId,
        Uuid assessmentId, long expectedRevision, string outcome, string rationale, IReadOnlyList<Uuid> acknowledged,
        Uuid? waiverId, CancellationToken ct) where TRequest : IRequestBase
    {
        if (commandId == Uuid.Empty || assessmentId == Uuid.Empty || expectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A decision requires a command id, an assessment and a non-negative expected revision.");
        var actor = ReadinessActor.From(context, tenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new ProgramReadiness(tenantId, programId, commandId), ct)
            .ConfigureAwait(false);
        var commandKind = kind == ReadinessDecisionKind.Readiness ? "DecideReadiness" : "DecideTypeIEntry";
        var provenance = ReadinessContent.Provenance(commandId, commandKind, snapshot.Stream, actorReference,
            (ulong)expectedRevision, ReadinessCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var frozen = await FreezeAsync(kind, snapshot, provenance, actor, tenantId, programId, assessmentId,
                expectedRevision, outcome, rationale, acknowledged, waiverId, now, ct).ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(frozen.Error!);
            (intent, response) = frozen.Value;
        }

        var result = await operations.ExecuteAsync(new NativeOperation<ProgramReadiness>(binding,
            () => new ProgramReadiness(tenantId, programId, commandId),
            program => program.Acceptance,
            program => ReadinessCommands.Prepared(program.PrepareDecision(expectedRevision, assessmentId, kind), response),
            (ProgramReadiness program, ImmutableContentRef acknowledgment) =>
                program.AcceptDecision(kind, assessmentId, provenance, intent!, acknowledgment)), context, ct)
            .ConfigureAwait(false);
        return await ReadinessContent.ResponseAsync<OperationAcceptedResponse>(content, tenantId, result, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        ReadinessDecisionKind kind, ProgramReadiness program, OperationProvenance provenance, ReadinessActor actor,
        Uuid tenantId, Uuid programId, Uuid assessmentId, long expectedRevision, string outcome, string rationale,
        IReadOnlyList<Uuid> acknowledged, Uuid? waiverId, DateTimeOffset now, CancellationToken ct)
    {
        var prepared = program.PrepareDecision(expectedRevision, assessmentId, kind);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);
        ReadinessReader.AssessmentContent loaded;
        IReadOnlyList<ReadinessGapView> gaps;
        string? decidedOutcome;
        var enteredType1 = false;
        var owners = new List<ReadinessGapPlan>();
        try
        {
            loaded = await readiness.ReadContentAsync(tenantId, programId, assessmentId, ct).ConfigureAwait(false)
                ?? throw new ImmutableContentIntegrityException("The adopted assessment has no recorded content.");
            var planned = new List<ReadinessGapView>(loaded.Gaps.Count);
            foreach (var gap in loaded.Gaps)
            {
                var owner = await reader.HydrateAsync(new ReadinessGapPlan(tenantId, programId, gap.GapId), ct)
                    .ConfigureAwait(false);
                owners.Add(owner);
                planned.Add(gap with { Plan = await readiness.ReadPlanAsync(tenantId, owner, ct).ConfigureAwait(false) });
            }

            gaps = planned;
            decidedOutcome = (await readiness.ReadDecisionAsync(tenantId, program, assessmentId, ct)
                .ConfigureAwait(false))?.Outcome;
            if (kind == ReadinessDecisionKind.TypeIEntry)
                enteredType1 = (await readiness.ReadEntryDecisionsAsync(tenantId, program, ct).ConfigureAwait(false))
                    .Any(static decision => decision.Outcome != ProgramReadiness.Defer);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail(RequestErrorKind.Conflict, $"The readiness content cannot be verified yet: {exception.Message}",
                true);
        }

        // The runner may decide only under an approved waiver scoped to this assessment, record kind and revision.
        ImmutableContentRef? waiverReference = null;
        if (waiverId is { } id)
        {
            var scope = new SeparationOfDutiesWaiverProofScope(tenantId, id, actor.MemberId, actor.MemberId,
                SeparationOfDutiesActions.Approve, kind == ReadinessDecisionKind.Readiness
                    ? SeparationOfDutiesRecordTypes.ReadinessAssessment
                    : SeparationOfDutiesRecordTypes.TypeIEntryDecision, assessmentId, assessmentId, expectedRevision);
            var proof = await waivers.ResolveAsync(scope, now, ct).ConfigureAwait(false);
            if (!proof.IsSuccess)
                return Fail(RequestErrorKind.Forbidden,
                    "The separation-of-duties waiver is not active for this member and assessment revision.");
            waiverReference = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.WaiverProofSchema,
                proof.Value!, ct).ConfigureAwait(false);
        }

        var independent = loaded.Fact.RunnerMemberId != actor.MemberId || waiverReference is not null;
        var failure = ReadinessDecisionRules.Validate(kind, outcome, rationale, gaps, acknowledged,
            loaded.Fact.RuleVersion, decidedOutcome, enteredType1, independent);
        if (failure is not null)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(failure.ToRequestError());

        SourceFactLocator assessmentProof;
        try
        {
            var assessment = new ReadinessAssessment(tenantId, programId, assessmentId);
            var built = await SourceFactProofs.BuildAsync<ReadinessAssessmentRecorded>(facts, content,
                ReadinessContent.Realm(tenantId), new NativeFactReference(assessment.Stream, loaded.Fact.Metadata!.EventId,
                    loaded.Fact.Metadata.AggregateVersion), ct).ConfigureAwait(false);
            assessmentProof = built.Locator;
        }
        catch (SourceFactVerificationException exception)
        {
            return Fail(RequestErrorKind.Conflict, $"The assessment fact cannot be proven yet: {exception.Message}", true);
        }

        var realm = tenantId.ToString();
        var ordered = gaps.OrderBy(gap => ReadinessEffects.GapKey(tenantId, programId, gap.GapId),
            Kernel.StreamKeyOrder.Instance).ToArray();
        var requirements = new List<SourceUseRequirement>(ordered.Length);
        foreach (var gap in ordered)
        {
            var owner = owners.First(item => item.GapId == gap.GapId);
            var latest = owner.Latest;
            var required = await ReadinessContent.WriteDeterministicAsync(content, tenantId,
                Uuid.CreateVersion5(provenance.RootOperationId, gap.GapId.ToString()), ReadinessContent.PlanFactsSchema,
                new ReadinessPlanFacts(gap.GapId, gap.Plan, null), ct).ConfigureAwait(false);
            requirements.Add(new SourceUseRequirement(owner.Stream,
                latest is null
                    ? new NativeFactReference(owner.Stream, Uuid.Empty, 0)
                    : NativeFactReference.Of(owner.Stream, latest),
                (ulong)owner.Revision, SourceUsePurpose.ReadinessDecision, required));
        }

        var gapManifest = await ImmutableSelectionManifests.WriteAsync<ReadinessGapView>(content, realm,
            Uuid.CreateVersion4(), ReadinessContent.GapItemSchema, ordered, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var acknowledgedManifest = await ImmutableSelectionManifests.WriteAsync<Uuid>(content, realm, Uuid.CreateVersion4(),
            ReadinessContent.AcknowledgedGapSchema, acknowledged.Distinct().OrderBy(
                static id => id.ToString(), StringComparer.Ordinal).ToArray(), SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var useManifest = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, realm,
            Uuid.CreateVersion4(), SourceUseRequirement.Schema, requirements, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var intent = await ReadinessContent.WriteAsync(content, tenantId, ReadinessDecisionIntent.Schema,
            new ReadinessDecisionIntent(ReadinessContent.Header(provenance, now), programId, assessmentId,
                assessmentProof, kind, outcome, rationale.Trim(), loaded.Fact.RuleVersion, gapManifest,
                acknowledgedManifest, useManifest, waiverReference), ct).ConfigureAwait(false);
        var acknowledgment = await ReadinessContent.AcceptedResponseAsync(content, tenantId, provenance.RootOperationId,
            RegisterFirmStaffHandler.StatusUrlTemplate, now, ct).ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, acknowledgment));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message,
        bool transient = false) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message, transient));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
