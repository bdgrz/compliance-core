using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     Applies one admitted participant result to its participant owner. The admission is found on the authoritative
///     campaign and must equal the proven source, intent, generation and participant; the result is applied exactly as
///     the frozen <see cref="ChildAdmissionIntent" /> says, with the original actor, never from the live campaign. An
///     exact replay returns the original fact, a result already applied under another command id is the same
///     application, and a participant held by a closure waits. An admission that is not visible yet is transient.
/// </summary>
sealed class ApplyPolicyParticipantResultHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, IDomainEventReader events)
    : IRequestHandler<ApplyPolicyParticipantResult, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyPolicyParticipantResult> context,
        CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ReceiverOutcome>> HandleCoreAsync(IRequestContext<ApplyPolicyParticipantResult> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.PersonId == Uuid.Empty ||
            command.Generation < 1 || command.ExpectedRevision is not null)
            return SourceUseReceiverSupport.Permanent("The application names another generation, operation or person.");
        var tenantId = PolicyCampaignContent.ParseTenant(command.Target.Realm);
        if (command.Target != PolicyCampaignParticipant.StreamOf(tenantId, command.CampaignId, command.PersonId))
            return SourceUseReceiverSupport.Permanent("The application names another participant stream.");

        var campaign = await new PolicyCampaignReader(reader, events).ReadAsync(tenantId, command.CampaignId,
            ct: ct).ConfigureAwait(false);
        if (campaign.AdmittedFor(command.OperationId) is not { } admitted)
            return SourceUseReceiverSupport.Transient("The admitted result is not visible yet.");
        if (command.Source.Stream != campaign.Stream || admitted.Metadata.EventId != command.Source.EventId ||
            admitted.Metadata.AggregateVersion != command.Source.AggregateVersion || admitted.Intent != command.Intent ||
            admitted.Generation != command.Generation || admitted.Operation.AffectedOwner != command.Target ||
            PolicyDistributionCampaign.KindOf(admitted) != ExpectedKind(command.ResultKind))
            return SourceUseReceiverSupport.Permanent("The source is not the admitted result of this participant.");

        var expectedHeader = PolicyCampaignContent.HeaderOf(admitted.Operation, campaign.Stream, command.Target,
            admitted.Operation.AcceptedAt);
        var provenance = PolicyCampaignContent.Child(command.CommandId, nameof(ApplyPolicyParticipantResult),
            command.Source.EventId, expectedHeader, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<PolicyCampaignParticipant, Built>(reader,
            () => new PolicyCampaignParticipant(tenantId, command.CampaignId, command.PersonId, command.CommandId, command.OperationId),
            owner => Decide(owner, binding), (_, token) => PrepareAsync(campaign, admitted, command, expectedHeader, token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var built = preparation.Value.Value!;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new PolicyCampaignParticipant(tenantId, command.CampaignId,
                    command.PersonId, command.CommandId, command.OperationId), participant =>
                    {
                        if (Decide(participant, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(decided)));
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
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(SourceUseReceiverSupport.Applied(
                                NativeFactReference.Of(participant.Stream, participant.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Outcome;
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The participant changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Result<Built>> PrepareAsync(PolicyCampaignSnapshot campaign, PolicyParticipantResultAdmitted admitted,
        ApplyPolicyParticipantResult command, DurableIntentHeader expectedHeader, CancellationToken ct)
    {
        var tenantId = campaign.TenantId;
        ChildAdmissionIntent intent;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ChildAdmissionIntent>(content, tenantId, command.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<Built>.Failure(Permanent($"The frozen admission cannot be verified: {exception.Message}"));
        }
        if (!PolicyCampaignHistory.Reference(intent.CanonicalAuthoredContent, command.ResultKind switch
        {
            CampaignParticipantResultKind.PolicyAcknowledgement => PolicyCampaignContent.AcknowledgeCommandSchema,
            CampaignParticipantResultKind.TrainingCompletion => PolicyCampaignContent.CompletionCommandSchema,
            _ => PolicyCampaignContent.WaiverCommandSchema,
        }) || intent.SelectedSourceProofs is not { Count: 1 } manifest ||
            manifest.ItemSchema != PolicyCampaignContent.AdmissionProofSchema ||
            !PolicyCampaignHistory.Reference(manifest.Content, PolicyCampaignContent.AdmissionProofSchema) ||
            manifest.ItemsSha256 != manifest.Content.Sha256 ||
            intent.Header != expectedHeader || intent.Header.RootOperationId != command.OperationId ||
            intent.ParentId != campaign.Id || intent.ChildId != command.OperationId ||
            intent.ChildKind != ChildKindOf(command.ResultKind) || intent.ParentGeneration != (ulong)admitted.Generation ||
            intent.ParentAdmissionRevision != (ulong)admitted.Revision || intent.Header.AffectedOwner != command.Target)
            return Result<Built>.Failure(Permanent("The application differs from the frozen admission intent."));

        var built = await BuildAsync(campaign, command, intent, ct).ConfigureAwait(false);
        return built.Error is null ? Result<Built>.Success(built) : Result<Built>.Failure(built.Error);
    }

    async ValueTask<Built> BuildAsync(PolicyCampaignSnapshot campaign, ApplyPolicyParticipantResult command,
        ChildAdmissionIntent intent, CancellationToken ct)
    {
        var tenantId = campaign.TenantId;
        var subject = campaign.Subject!;
        var header = intent.Header;
        var at = header.AcceptedAt;
        try
        {
            var proofs = await PolicyCampaignContent.ReadManifestAsync<PolicyParticipantAdmissionProof>(content, tenantId,
                intent.SelectedSourceProofs, ct).ConfigureAwait(false);
            if (proofs.Count != 1)
                return Built.Failed(Permanent("The admission must name its exact material and including audience fact."));
            var proof = proofs[0];
            if (proof is null || proof.Material != NativeFactReference.Of(campaign.Stream, campaign.Launch!) ||
                proof.Audience is null || proof.Audience.Stream != campaign.Stream ||
                campaign.Facts.SingleOrDefault(value => value.Metadata.EventId == proof.Audience.EventId) is not { } audience ||
                audience.Metadata.AggregateVersion != proof.Audience.AggregateVersion ||
                audience is not (PolicyCampaignAudienceFrozen or PolicyCampaignAudienceAmended))
                return Built.Failed(Permanent("The admission source proof names another material or audience event."));
            var includesPerson = audience switch
            {
                PolicyCampaignAudienceFrozen frozen => (await PolicyCampaignContent.ReadAsync<IReadOnlyList<CampaignAudienceEntry>>(
                    content, tenantId, frozen.EntriesManifest, ct).ConfigureAwait(false)).Any(value => value.PersonId == command.PersonId),
                PolicyCampaignAudienceAmended amended => (await PolicyCampaignContent.ReadAsync<IReadOnlyList<CampaignAudienceAmendment>>(
                    content, tenantId, amended.AmendmentsManifest, ct).ConfigureAwait(false)).Any(value =>
                        value.PersonId == command.PersonId && value.Reason is "joiner" or "mover_in"),
                _ => false,
            };
            if (!includesPerson)
                return Built.Failed(Permanent("The exact audience source proof did not include this participant."));
            switch (command.ResultKind)
            {
                case CampaignParticipantResultKind.PolicyAcknowledgement:
                    {
                        var request = await PolicyCampaignContent.ReadAsync<AcknowledgePolicy>(content, tenantId,
                            intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
                        if (request.PersonId != command.PersonId || request.TenantId != tenantId || request.ProgramId != campaign.ProgramId ||
                            request.CampaignId != campaign.Id || request.CommandId != command.OperationId ||
                            !Original(intent, request.ExpectedCampaignRevision, request, PolicyCampaignContent.AcknowledgeCommandSchema) ||
                            proof.Performer is not { } performer)
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
                        if (request.PersonId != command.PersonId || request.TenantId != tenantId || request.ProgramId != campaign.ProgramId ||
                            request.CampaignId != campaign.Id || request.CommandId != command.OperationId ||
                            !Original(intent, request.ExpectedCampaignRevision, request, PolicyCampaignContent.CompletionCommandSchema))
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
                        if (request.PersonId != command.PersonId || request.TenantId != tenantId || request.ProgramId != campaign.ProgramId ||
                            request.CampaignId != campaign.Id || request.CommandId != command.OperationId ||
                            !Original(intent, request.ExpectedCampaignRevision, request, PolicyCampaignContent.WaiverCommandSchema) ||
                            header.OriginalActor.Kind != "member" || !Uuid.TryParse(header.OriginalActor.Id, System.Globalization.CultureInfo.InvariantCulture,
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
            ct.ThrowIfCancellationRequested();
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

    static bool Original<T>(ChildAdmissionIntent intent, long expected, T request, string schema) where T : notnull =>
        expected >= 0 && intent.Header.OriginalExpectedRevision == (ulong)expected &&
        intent.CanonicalAuthoredContent.Schema == schema && AuthoredIntentDigest.Compute(request) == intent.Header.AuthoredIntentSha256;

    static Result<ReceiverOutcome>? Decide(PolicyCampaignParticipant owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        return owner.SelectedResult is { } result
            ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, result))
            : null;
    }

    sealed record Step(Result<ReceiverOutcome> Outcome);

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
