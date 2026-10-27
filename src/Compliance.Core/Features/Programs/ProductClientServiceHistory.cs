using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Validates every native transition transiently before returning a bounded command owner or full read history.</summary>
public static class ProductClientServiceHistory
{
    public static async Task<ProductClientServiceHistorySnapshot> ReadAsync(IDomainEventReader events, ClientService owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var replay = new ClientService(owner.TenantId, owner.Id, owner.Acceptance.SelectedLogicalId);
        var logical = new HashSet<Uuid>();
        var known = NativeReplayCommitment.Empty;
        foreach (var fact in facts)
        {
            if (fact.Metadata.AggregateVersion <= owner.NativeCut.KnownThroughVersion)
                known = NativeReplayCommitment.Append(known, fact, true);
            if (fact is not IProductClientServiceFact native || native.Operation is null || !logical.Add(native.Operation.LogicalId))
                throw new ImmutableContentIntegrityException("The client service prefix contains another fact kind or repeated logical acceptance.");
            try
            {
                ValidateHeader(native);
                replay.ObserveVerified(fact);
            }
            catch (InvalidOperationException error)
            {
                throw new ImmutableContentIntegrityException("The client service prefix contains an invalid native transition: " + error.Message);
            }
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw new ImmutableContentIntegrityException("The selected client service acceptance differs from its actual payload and full metadata.");
        }
        if (replay.IsCreated != owner.IsCreated || replay.Revision != owner.Revision || replay.NativeSequence != owner.NativeSequence ||
            replay.CurrentContent != owner.CurrentContent || replay.ProgramId != owner.ProgramId || replay.IsActive != owner.IsActive ||
            !Same((DomainEvent?)replay.CurrentValueFact, (DomainEvent?)owner.CurrentValueFact) || !Same(replay.Retirement, owner.Retirement))
            throw new ImmutableContentIntegrityException("The verified client service prefix differs from compact current authority.");
        owner.NativeCut.Confirm(owner, replay.NativeCut.Digest, replay.NativeCut.MetadataDigest, known);
        ct.ThrowIfCancellationRequested();
        return new ProductClientServiceHistorySnapshot(owner, facts);
    }

    public static void ValidateHeader(IProductClientServiceFact fact)
    {
        var owner = new ClientService(fact.TenantId, fact.ServiceId);
        var operation = fact.Operation;
        var kind = fact switch
        {
            ClientServiceCreated => nameof(CreateClientService),
            ClientServiceRevised => nameof(ReviseClientService),
            ClientServiceRetired => nameof(RetireClientService),
            _ => throw new ImmutableContentIntegrityException("The service has another native fact kind."),
        };
        if (fact.TenantId == Uuid.Empty || fact.ServiceId == Uuid.Empty || fact.ProgramId == Uuid.Empty ||
            fact.Revision < 1 || fact.NativeSequence < 1 || operation is null || operation.Actor is null ||
            operation.Actor.Display is null || operation.Actor.Kind != "member" || operation.AuthoredIntentSha256 is not { Length: 64 } ||
            !Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var member) || member == Uuid.Empty ||
            !NativeOwnerHistory.IsOwnRoot(owner, operation, kind, operation.OriginalExpectedRevision) ||
            (fact is ClientServiceCreated ? operation.LogicalId != owner.Id || operation.OriginalExpectedRevision is not null
                : operation.OriginalExpectedRevision is not { } expected || expected > long.MaxValue) ||
            fact.ChangedAt != operation.AcceptedAt || !Reference(fact.Content, ProductClientServiceContent.Schema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1") ||
            fact is ClientServiceRetired retired && !Reference(retired.Retirement, ProductClientServiceRetirement.Schema))
            throw new ImmutableContentIntegrityException("The service header differs from its exact native root, scope, member or immutable references.");
    }

    static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null &&
        reference.ContentId != Uuid.Empty && reference.Bytes > 0 && reference.Sha256 is { Length: 64 } &&
        reference.Sha256.All(Uri.IsHexDigit) && reference.Schema == schema && reference.MediaType == "application/json";

    static bool Same(DomainEvent? actual, DomainEvent? expected) => actual is null ? expected is null
        : expected is not null && actual.GetType() == expected.GetType() && actual.Equals(expected) && actual.Metadata == expected.Metadata;
}
