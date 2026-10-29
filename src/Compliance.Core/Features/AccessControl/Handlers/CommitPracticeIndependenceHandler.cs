using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Commits the permanent practice wall of one reserved subject. The source is verified to be the irreversible
///     authorization of the engagement owner, so no commitment is ever made while any subject reservation can still
///     fail, and the exact active reservation of the operation must be held. The commitment survives every later
///     outcome: nothing in this owner removes it.
/// </summary>
sealed class CommitPracticeIndependenceHandler(IAggregateExecutor executor, TimeProvider clock,
    EngagementAcceptanceVerifier verifier, IAggregateReader reader, IImmutableContentStore content)
    : IRequestHandler<CommitPracticeIndependence, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CommitPracticeIndependence> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != EngagementAcceptanceEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The commitment names another operation or generation.");
        var loaded = await verifier.LoadAsync(command.Source, command.Intent, command.OperationId, Uuid.Empty,
            command.CommandId, ct, OperationSource.Authorization).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, intent) = (loaded.Owner!, loaded.Intent!);
        var subject = loaded.Subjects!.SingleOrDefault(item => item.Stream == command.Target);
        if (subject is null || subject.Kind != command.SubjectKind || subject.SubjectId != command.SubjectId ||
            subject.Practice != command.Practice)
            return SourceUseReceiverSupport.Permanent("The commitment differs from the frozen practice subject.");

        var tenantId = EngagementIndependenceIntent.TenantOf(intent.Header);
        var engagementId = Uuid.Parse(owner.Stream.Resource, CultureInfo.InvariantCulture);
        var provenance = EngagementAcceptanceEffects.Child(command.CommandId, nameof(CommitPracticeIndependence),
            command.Source.EventId, intent, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var key = EngagementAcceptanceEffects.UseKey(command.OperationId, owner.Stream, command.Target);
        Result<ReceiverOutcome>? Decide(PracticeIndependenceCommitment target)
        {
            switch (target.Acceptance.Decide(binding))
            {
                case NativeAcceptanceDecision.Replayed:
                    return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(target.Stream, target.Acceptance.SelectedFact!));
                case NativeAcceptanceDecision.Conflict:
                    return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
            }
            if (target.CommitmentOf(key) is not { } existing)
                return null;
            return existing.Intent == command.Intent && existing.Practice == command.Practice && existing.EngagementId == engagementId
                ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(target.Stream, existing))
                : SourceUseReceiverSupport.Permanent("The permanent commitment differs from the exact frozen authorization.");
        }
        var prepared = await NativeReceiverPreparation.PrepareAsync<PracticeIndependenceCommitment, PreparedCut>(reader,
            () => new PracticeIndependenceCommitment(tenantId, subject.Kind, subject.SubjectId,
                command.CommandId, selectedReservation: key).ForSourceUses(key), Decide,
            async (target, token) =>
            {
                if (target.Uses.Find(key) is not { IsActive: true, GrantFact: PracticeIndependenceReserved grant })
                    return Result<PreparedCut>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact live practice reservation is not visible yet.", true));
                if (grant.Intent.Schema != SourceUseAuthorizationIntent.Schema)
                    return Result<PreparedCut>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The live practice grant has another source-use schema."));
                var frozen = await content.ReadJsonAsync<SourceUseAuthorizationIntent>(command.Target.Realm, grant.Intent, token)
                    .ConfigureAwait(false);
                if (frozen.Header != intent.Header ||
                    frozen.ParentIntent != command.Intent || frozen.Reservation != key || frozen.Grant is not null ||
                    frozen.Disposition is not null || frozen.Requirement is null || frozen.Requirement.SourceOwner != command.Target ||
                    frozen.Requirement.Purpose != SourceUsePurpose.ActualStaffAssignment ||
                    frozen.Requirement.ContentEvent != new NativeFactReference(command.Target, Uuid.Empty, 0) ||
                    frozen.Requirement.ResourceRevision != 0 || frozen.Requirement.RequiredFacts is null ||
                    frozen.Requirement.RequiredFacts.Schema != PracticeSubjectIntent.Schema)
                    return Result<PreparedCut>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The live practice grant is not the exact authorization's source-use envelope."));
                var frozenSubject = await content.ReadJsonAsync<PracticeSubjectIntent>(command.Target.Realm,
                    frozen.Requirement.RequiredFacts, token).ConfigureAwait(false);
                if (frozenSubject != subject)
                    return Result<PreparedCut>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The live practice grant names another frozen subject or practice."));
                return Result<PreparedCut>.Success(new PreparedCut(target.Version, target.CommittedStreamPosition,
                    target.ReplayCut.Digest));
            }, ct).ConfigureAwait(false);
        if (!prepared.IsSuccess)
            return Result<ReceiverOutcome>.Failure(prepared.Error!);
        if (prepared.Value!.Outcome is { } known)
            return known;
        var cut = prepared.Value.Value!;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, subject.Kind,
                    subject.SubjectId, command.CommandId, selectedReservation: key), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.CommitmentOf(key) is { } existing)
                            return existing.Intent == command.Intent && existing.Practice == command.Practice && existing.EngagementId == engagementId
                                ? AggregateOutcome.Discard(Result<Step>.Success(new Step(NativeFactReference.Of(fresh.Stream, existing), false)))
                                : AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                    "The permanent commitment differs from the exact frozen authorization.")));
                        if ((fresh.Version, fresh.CommittedStreamPosition, fresh.ReplayCut.Digest) !=
                            (cut.Version, cut.Position, cut.Digest))
                            return AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The prepared practice reservation prefix changed before commitment.", true)));
                        var committed = fresh.Commit(provenance, command.Intent, command.Generation, command.Practice,
                            engagementId);
                        return committed.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(committed.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The owner changed concurrently; rehydrate and decide again.
            }
        }
    }

    sealed record PreparedCut(ulong Version, ulong Position, string Digest);

    sealed record Step(NativeFactReference Fact, bool Applied);
}
