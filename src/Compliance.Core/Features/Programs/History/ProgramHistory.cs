using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.History;

/// <summary>Validates every native transition transiently before returning a bounded command owner or full read history.</summary>
public static class ProgramHistory
{
    public static async Task<ProgramHistorySnapshot> ReadAsync(IDomainEventReader events, ComplianceProgram owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        if (facts.Any(fact => fact is ProgramCreationAttemptFenced))
        {
            await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
            {
                if (record.ResourceOffset >= owner.CommittedStreamPosition)
                    break;
                if (record.Event is ProgramCreationAttemptFenced fence && fence.ExpectedPosition != record.ResourceOffset)
                    throw new ImmutableContentIntegrityException("A creation fence differs from its exact physical source position.");
            }
        }
        var replay = new ComplianceProgram(owner.TenantId, owner.Id, owner.Acceptance.SelectedLogicalId);
        var logical = new HashSet<Uuid>();
        var known = NativeReplayCommitment.Empty;
        foreach (var fact in facts)
        {
            if (fact.Metadata.AggregateVersion <= owner.NativeCut.KnownThroughVersion)
                known = NativeReplayCommitment.Append(known, fact, true);
            if (fact is ProgramCreationAttemptFenced)
            {
                try
                {
                    replay.ObserveVerified(fact);
                }
                catch (InvalidOperationException error)
                {
                    throw new ImmutableContentIntegrityException("The program prefix contains an invalid creation fence: " + error.Message);
                }
                continue;
            }
            if (fact is not IProgramFact native || native.Operation is null || !logical.Add(native.Operation.LogicalId))
                throw new ImmutableContentIntegrityException("The program prefix contains another fact kind or repeated logical acceptance.");
            try
            {
                ValidateHeader(native);
                replay.ObserveVerified(fact);
            }
            catch (InvalidOperationException error)
            {
                throw new ImmutableContentIntegrityException("The program prefix contains an invalid native transition: " + error.Message);
            }
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw new ImmutableContentIntegrityException("The selected program acceptance differs from its actual payload and full metadata.");
        }
        if (replay.IsCreated != owner.IsCreated || replay.Revision != owner.Revision || replay.NativeSequence != owner.NativeSequence ||
            replay.CurrentContent != owner.CurrentContent || replay.CriteriaEditionId != owner.CriteriaEditionId ||
            replay.InitialCriteriaPublication != owner.InitialCriteriaPublication ||
            replay.CreationGeneration != owner.CreationGeneration ||
            replay.FencedCreationIntent != owner.FencedCreationIntent || replay.FencedCreationActor != owner.FencedCreationActor ||
            replay.LastChangeWasSelection != owner.LastChangeWasSelection || !Same(replay.CurrentSelection, owner.CurrentSelection))
            throw new ImmutableContentIntegrityException("The verified program prefix differs from compact current authority.");
        owner.NativeCut.Confirm(owner, replay.NativeCut.Digest, replay.NativeCut.MetadataDigest, known);
        ct.ThrowIfCancellationRequested();
        return new ProgramHistorySnapshot(owner, facts);
    }

    public static void ValidateHeader(IProgramFact fact)
    {
        var owner = new ComplianceProgram(fact.TenantId, fact.ProgramId);
        var operation = fact.Operation;
        var kind = fact switch
        {
            ProgramCreated => nameof(CreateProgram),
            ProgramRevised => nameof(ReviseProgram),
            ProgramCriteriaEditionSelected => nameof(SelectProgramCriteriaEdition),
            _ => throw new ImmutableContentIntegrityException("The program has another native fact kind."),
        };
        if (fact.TenantId == Uuid.Empty || fact.ProgramId == Uuid.Empty || fact.Revision < 1 || fact.NativeSequence < 1 ||
            fact.ChangeCount is < 0 or > 1 || fact.ChangeCount == 0 && fact is not ProgramCriteriaEditionSelected ||
            operation is null || operation.Actor is null || operation.Actor.Display is null || operation.Actor.Kind != "member" ||
            operation.AuthoredIntentSha256 is not { Length: 64 } ||
            !Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var member) || member == Uuid.Empty ||
            !NativeOwnerHistory.IsOwnRoot(owner, operation, kind, operation.OriginalExpectedRevision) ||
            (fact is ProgramCreated ? operation.LogicalId != owner.Id || operation.OriginalExpectedRevision is not null
                : operation.OriginalExpectedRevision is not { } expected || expected > long.MaxValue) ||
            !Reference(fact.Content, ProgramContent.Schema) || !Reference(fact.FirstResponse, FirstResponses.Schema) ||
            !Reference(fact.OperationResult, "mutation-result-content.v1"))
            throw new ImmutableContentIntegrityException("The program header differs from its exact native root, member or immutable references.");
        if (fact is ProgramCriteriaEditionSelected selection && operation.AuthoredIntentSha256 !=
            AuthoredIntentDigest.Compute(new SelectProgramCriteriaEdition(operation.LogicalId, fact.TenantId, fact.ProgramId,
                (long)operation.OriginalExpectedRevision!.Value, selection.EditionId)))
            throw new ImmutableContentIntegrityException("The program criteria selection differs from its exact authored command binding.");
        if (fact is ProgramCriteriaEditionSelected selected &&
            (selected.CriteriaPublication is null) != (selected.AccessAdmission is null))
            throw new ImmutableContentIntegrityException("The program criteria selection has partial admission evidence.");
        if (fact is ProgramCriteriaEditionSelected { CriteriaPublication: { } selectionProof } selectedWithProof &&
            (selectionProof.EditionId != selectedWithProof.EditionId ||
             selectionProof.EditionId == Uuid.Empty || selectionProof.EventId == Uuid.Empty ||
             !ProgramAdmissionEvidence.Matches(selectedWithProof.AccessAdmission, fact.TenantId,
                 operation.LogicalId, owner.Stream, operation)))
            throw new ImmutableContentIntegrityException("The program criteria selection has inconsistent publication evidence.");
        if (fact is ProgramCreated created && ((created.CriteriaEditionId is null) != (created.CriteriaPublication is null) ||
            (created.CriteriaPublication is null) != (created.AccessAdmission is null) ||
            created.CriteriaPublication is { } publication && (publication.EditionId == Uuid.Empty ||
                publication.EventId == Uuid.Empty || created.CriteriaEditionId != publication.EditionId ||
                !ProgramAdmissionEvidence.Matches(created.AccessAdmission, fact.TenantId,
                    owner.Id, owner.Stream, operation, created.CreationGeneration))))
            throw new ImmutableContentIntegrityException("The program creation has inconsistent publication evidence.");
    }

    static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null &&
        reference.ContentId != Uuid.Empty && reference.Bytes > 0 && reference.Sha256 is { Length: 64 } &&
        reference.Sha256.All(Uri.IsHexDigit) && reference.Schema == schema && reference.MediaType == "application/json";

    static bool Same(DomainEvent? actual, DomainEvent? expected) => actual is null ? expected is null
        : expected is not null && actual.GetType() == expected.GetType() && actual.Equals(expected) && actual.Metadata == expected.Metadata;
}
