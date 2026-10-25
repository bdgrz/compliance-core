using Bdgrz.Compliance.Features.Kernel;
using System.Globalization;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Validates every native transition transiently before returning a bounded command owner or full read history.</summary>
public static class ProgramHistory
{
    public static async Task<ProgramHistorySnapshot> ReadAsync(IDomainEventReader events, ComplianceProgram owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var replay = new ComplianceProgram(owner.TenantId, owner.Id, owner.Acceptance.SelectedLogicalId);
        var logical = new HashSet<Uuid>();
        var known = NativeReplayCommitment.Empty;
        foreach (var fact in facts)
        {
            if (fact.Metadata.AggregateVersion <= owner.NativeCut.KnownThroughVersion)
                known = NativeReplayCommitment.Append(known, fact, true);
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
    }

    static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null &&
        reference.ContentId != Uuid.Empty && reference.Bytes > 0 && reference.Sha256 is { Length: 64 } &&
        reference.Sha256.All(Uri.IsHexDigit) && reference.Schema == schema && reference.MediaType == "application/json";

    static bool Same(DomainEvent? actual, DomainEvent? expected) => actual is null ? expected is null
        : expected is not null && actual.GetType() == expected.GetType() && actual.Equals(expected) && actual.Metadata == expected.Metadata;
}
