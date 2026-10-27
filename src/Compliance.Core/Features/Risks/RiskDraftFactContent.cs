using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public static class RiskDraftFactContent
{
    public const string Schema = "risk-draft-revision-content.v1";
    public static RiskDraftAuthorityHeader Header(RiskDraftRevisionView value) => new(value.TenantId, value.ProgramId,
        value.RiskId, value.Identifier, value.Revision, value.ChangedByMemberId, value.ChangedByDisplay, value.Actor, value.ChangedAt);
    public static async ValueTask<RiskDraftRevisionView> ReadAsync(IImmutableContentStore content, IRiskDraftFact fact, CancellationToken ct = default)
    {
        ValidateNative((DomainEvent)fact);
        var header = fact.Header;
        var value = await content.ReadJsonAsync<RiskDraftRevisionView>(header.TenantId.ToString(), fact.Content, ct).ConfigureAwait(false);
        if (value is null || Header(value) != header || RiskDraftRules.Validate(value.Identifier, value.Content) is not null ||
            ContentDigest.OfJson(value.Content) != ContentDigest.OfJson(RiskDraftRules.Clean(value.Content)))
            throw new ImmutableContentIntegrityException("The risk draft immutable narrative differs from its native authority.");
        return value;
    }
    public static void ValidateNative(DomainEvent value)
    {
        if (value is not IRiskDraftFact fact || fact.Header is not { } header || fact.Operation is not { } operation ||
            !Reference(fact.Content, Schema) || header.TenantId == Uuid.Empty || header.ProgramId == Uuid.Empty ||
            header.RiskId == Uuid.Empty || header.Revision < 1 || fact.NativeSequence != header.Revision ||
            RiskDraftRules.ValidateIdentifier(header.Identifier) is not null ||
            header.RiskId != RiskDraft.IdFor(header.TenantId, header.ProgramId, header.Identifier) ||
            header.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(header.ActorDisplay) || header.Actor is null ||
            operation.Actor is null || operation.Actor != header.Actor || operation.AcceptedAt != header.ChangedAt ||
            operation.LogicalId == Uuid.Empty || operation.RootOperationId != operation.LogicalId || operation.Episode != header.RiskId.ToString() ||
            operation.AuthoredIntentSha256 is not { Length: 64 } || !operation.AuthoredIntentSha256.All(Uri.IsHexDigit) ||
            operation.Owner != new EventStreamAddress(header.TenantId.ToString(), RiskDraft.Area, header.RiskId.ToString()) ||
            operation.FirstOwner != operation.Owner || operation.AffectedOwner != operation.Owner || operation.ExpectedRevisionOwner != operation.Owner ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1") ||
            value is RiskDraftCreated && (header.Revision != 1 || operation.LogicalId != header.RiskId || operation.CommandKind != nameof(CreateRiskDraft) ||
                operation.OriginalExpectedRevision is not null) ||
            value is RiskDraftRevised && (header.Revision < 2 || operation.CommandKind != nameof(ReviseRiskDraft) ||
                operation.OriginalExpectedRevision != unchecked((ulong)(header.Revision - 1))) ||
            value is not RiskDraftCreated and not RiskDraftRevised || DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
            throw new ImmutableContentIntegrityException("The risk draft native source has another exact owner, actor, intent, gate or schema.");
    }
    public static DomainEventMetadata Metadata(DomainEvent value)
    {
        DomainEventMetadata metadata;
        try
        { metadata = value.Metadata; }
        catch (InvalidOperationException) { throw new ImmutableContentIntegrityException("The risk draft native source carries no committed metadata."); }
        if (value is not IRiskDraftFact fact || metadata.IsAudit || metadata.EventId == Uuid.Empty ||
            metadata.AggregateId != fact.Header.RiskId || metadata.AggregateVersion == 0 || metadata.OccurredOn == default)
            throw new ImmutableContentIntegrityException("The risk draft native source metadata has another exact aggregate binding.");
        return metadata;
    }
    static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.ContentId != Uuid.Empty && value.Bytes >= 0 &&
        value.Schema == schema && value.MediaType == ImmutableContentStoreExtensions.JsonMediaType && value.Sha256 is { Length: 64 } && value.Sha256.All(Uri.IsHexDigit);
    public static RiskDraftView View(RiskDraftRevisionView revision) => new(revision.TenantId, revision.ProgramId,
        revision.RiskId, revision.Identifier, revision.Revision, "draft_unassessed", "unresolved", revision.Content,
        revision.ChangedByMemberId, revision.ChangedByDisplay, revision.ChangedAt)
    { LastChangedBy = revision.Actor };
}
