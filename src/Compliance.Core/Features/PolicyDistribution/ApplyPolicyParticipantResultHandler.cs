using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Applies one admitted participant result to its participant owner. The admission is found on the authoritative
///     campaign and must equal the proven source, intent, generation and participant; the result is applied exactly as
///     the frozen <see cref="ChildAdmissionIntent" /> says, with the original actor, never from the live campaign. An
///     exact replay returns the original fact, a result already applied under another command id is the same
///     application, and a participant held by a closure waits. An admission that is not visible yet is transient.
/// </summary>
sealed class ApplyPolicyParticipantResultHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ApplyPolicyParticipantResult, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyPolicyParticipantResult> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.PersonId == Uuid.Empty ||
            command.Generation < 1 || command.ExpectedRevision is not null)
            return SourceUseReceiverSupport.Permanent("The application names another generation, operation or person.");
        var tenantId = PolicyCampaignContent.ParseTenant(command.Target.Realm);
        if (command.Target != PolicyCampaignParticipant.StreamOf(tenantId, command.CampaignId, command.PersonId))
            return SourceUseReceiverSupport.Permanent("The application names another participant stream.");

        var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId, command.CampaignId), ct)
            .ConfigureAwait(false);
        if (campaign.AdmittedFor(command.OperationId) is not { } admitted)
            return SourceUseReceiverSupport.Transient("The admitted result is not visible yet.");
        if (command.Source.Stream != campaign.Stream || admitted.Metadata.EventId != command.Source.EventId ||
            admitted.Metadata.AggregateVersion != command.Source.AggregateVersion || admitted.Intent != command.Intent ||
            admitted.Generation != command.Generation || admitted.Operation.AffectedOwner != command.Target ||
            PolicyDistributionCampaign.KindOf(admitted) != ExpectedKind(command.ResultKind))
            return SourceUseReceiverSupport.Permanent("The source is not the admitted result of this participant.");

        ChildAdmissionIntent intent;
        PolicyParticipantAdmissionProof proof;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ChildAdmissionIntent>(content, tenantId, command.Intent, ct)
                .ConfigureAwait(false);
            proof = (await PolicyCampaignContent.ReadManifestAsync<PolicyParticipantAdmissionProof>(content, tenantId,
                intent.SelectedSourceProofs, ct).ConfigureAwait(false)).Single();
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen admission cannot be verified: {exception.Message}");
        }

        if (intent.Header.RootOperationId != command.OperationId || intent.ParentId != campaign.Id ||
            intent.ChildId != command.OperationId || intent.ChildKind != ChildKindOf(command.ResultKind) ||
            intent.ParentGeneration != (ulong)admitted.Generation || intent.Header.AffectedOwner != command.Target)
            return SourceUseReceiverSupport.Permanent("The application differs from the frozen admission intent.");

        var provenance = PolicyCampaignContent.Child(command.CommandId, nameof(ApplyPolicyParticipantResult),
            command.Source.EventId, intent.Header, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var built = await BuildAsync(campaign, command, intent, proof, ct).ConfigureAwait(false);
        if (built.Error is not null)
            return Result<ReceiverOutcome>.Failure(built.Error);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new PolicyCampaignParticipant(tenantId, command.CampaignId,
                    command.PersonId, command.CommandId), participant =>
                    {
                        switch (participant.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(new Step(
                                    NativeFactReference.Of(participant.Stream, participant.Acceptance.SelectedFact!),
                                    false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (participant.ResultFor(command.OperationId) is { } applied)
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(NativeFactReference.Of(participant.Stream, applied), false)));
                        var raised = command.ResultKind switch
                        {
                            CampaignParticipantResultKind.PolicyAcknowledgement => participant.RecordAcknowledgement(
                                provenance, built.PolicyId, built.Version, built.Sha, built.Text, built.Performer!,
                                built.Actor, built.OnBehalf, built.At, command.Intent, built.Result!),
                            CampaignParticipantResultKind.TrainingCompletion => participant.RecordCompletion(provenance,
                                built.PolicyId, built.Version, built.CompletedOn, built.Source!, built.Evidence!,
                                built.Actor, built.At, command.Intent, built.Result!),
                            _ => participant.ApproveWaiver(provenance, built.Reason!, built.ExpiresOn, built.Actor,
                                built.ApproverMemberId, built.At, command.Intent, built.Result!),
                        };
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(participant.Stream, participant.Acceptance.SelectedFact!),
                                true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The participant changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Built> BuildAsync(PolicyDistributionCampaign campaign, ApplyPolicyParticipantResult command,
        ChildAdmissionIntent intent, PolicyParticipantAdmissionProof proof, CancellationToken ct)
    {
        var tenantId = campaign.TenantId;
        var subject = campaign.Subject!;
        var header = intent.Header;
        var at = header.AcceptedAt;
        try
        {
            switch (command.ResultKind)
            {
                case CampaignParticipantResultKind.PolicyAcknowledgement:
                    {
                        var request = await PolicyCampaignContent.ReadAsync<AcknowledgePolicy>(content, tenantId,
                            intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
                        if (request.PersonId != command.PersonId || proof.Performer is not { } performer)
                            return Built.Failed(Permanent("The acknowledgement differs from its frozen admission."));
                        var view = new CampaignAcknowledgementView(command.OperationId, command.PersonId,
                            request.PolicyVersion, request.ContentSha256, request.AcknowledgementText, performer,
                            header.OriginalActor, proof.RecordedOnBehalf, at);
                        var result = await ResultAsync(tenantId, command, PolicyCampaignContent.AcknowledgementSchema, view,
                            ct).ConfigureAwait(false);
                        return new Built(null, subject.RecordId, request.PolicyVersion, request.ContentSha256,
                            request.AcknowledgementText, performer, header.OriginalActor, proof.RecordedOnBehalf, at,
                            default, null, null, null, default, default, result);
                    }

                case CampaignParticipantResultKind.TrainingCompletion:
                    {
                        var request = await PolicyCampaignContent.ReadAsync<RecordTrainingCompletion>(content, tenantId,
                            intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
                        if (request.PersonId != command.PersonId)
                            return Built.Failed(Permanent("The completion differs from its frozen admission."));
                        var evidence = request.EvidenceReference.Trim();
                        var view = new CampaignCompletionView(command.OperationId, command.PersonId,
                            request.RequirementVersion, request.CompletedOn, request.Source, evidence,
                            header.OriginalActor, at);
                        var result = await ResultAsync(tenantId, command, PolicyCampaignContent.CompletionSchema, view, ct)
                            .ConfigureAwait(false);
                        return new Built(null, subject.RecordId, request.RequirementVersion, string.Empty, string.Empty, null,
                            header.OriginalActor, false, at, request.CompletedOn, request.Source, evidence, null, default,
                            default, result);
                    }

                default:
                    {
                        var request = await PolicyCampaignContent.ReadAsync<ApproveCampaignWaiver>(content, tenantId,
                            intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
                        if (request.PersonId != command.PersonId ||
                            !Uuid.TryParse(header.OriginalActor.Id, System.Globalization.CultureInfo.InvariantCulture,
                                out var approverMemberId))
                            return Built.Failed(Permanent("The exception differs from its frozen admission."));
                        var reason = request.Reason.Trim();
                        var view = new CampaignWaiverView(command.OperationId, command.PersonId, reason,
                            request.ExpiresOn, header.OriginalActor, at);
                        var result = await ResultAsync(tenantId, command, PolicyCampaignContent.WaiverSchema, view, ct)
                            .ConfigureAwait(false);
                        return new Built(null, Uuid.Empty, 0, string.Empty, string.Empty, null, header.OriginalActor, false,
                            at, default, null, null, reason, request.ExpiresOn, approverMemberId, result);
                    }
            }
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Built.Failed(Permanent($"The frozen authored result cannot be verified: {exception.Message}"));
        }
    }

    async Task<ImmutableContentRef> ResultAsync<T>(Uuid tenantId, ApplyPolicyParticipantResult command, string schema,
        T view, CancellationToken ct) where T : notnull
    {
        var body = await PolicyCampaignContent.WriteDerivedAsync(content, tenantId, command.OperationId,
            "participant-result-view", schema, view, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.WriteDerivedResultAsync(content, tenantId, command.OperationId, schema,
            body, ct).ConfigureAwait(false);
    }

    static CampaignParticipantResultKind ExpectedKind(CampaignParticipantResultKind kind) => kind;

    static AdmittedChildKind ChildKindOf(CampaignParticipantResultKind kind) => kind switch
    {
        CampaignParticipantResultKind.PolicyAcknowledgement => AdmittedChildKind.PolicyAcknowledgement,
        CampaignParticipantResultKind.TrainingCompletion => AdmittedChildKind.TrainingCompletion,
        _ => AdmittedChildKind.CampaignWaiver,
    };

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    sealed record Step(NativeFactReference Fact, bool Applied);

    /// <summary>The values one typed result applies; the unused ones of the other variants stay empty.</summary>
    sealed record Built(RequestError? Error, Uuid PolicyId, long Version, string Sha, string Text,
        ActorReference? Performer, ActorReference Actor, bool OnBehalf, DateTimeOffset At, DateOnly CompletedOn,
        string? Source, string? Evidence, string? Reason, DateOnly ExpiresOn, Uuid ApproverMemberId,
        ImmutableContentRef? Result)
    {
        public static Built Failed(RequestError error) => new(error, Uuid.Empty, 0, string.Empty, string.Empty, null,
            null!, false, default, default, null, null, null, default, Uuid.Empty, null);
    }
}
