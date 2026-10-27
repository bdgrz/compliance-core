using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.draft.created", 2)]
public sealed record RiskDraftCreated(RiskDraftAuthorityHeader Header, ImmutableContentRef Content,
    long NativeSequence, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, IRiskDraftFact
{
    public Uuid TenantId => Header.TenantId;
    public Uuid ProgramId => Header.ProgramId;
    public Uuid RiskId => Header.RiskId;
    public string Identifier => Header.Identifier;
    public long Revision => Header.Revision;
    public Uuid ActorMemberId => Header.ActorMemberId;
    public string ActorDisplay => Header.ActorDisplay;
    public DateTimeOffset ChangedAt => Header.ChangedAt;
    public ActorReference Actor => Header.Actor;
    [JsonIgnore]
    public ActorReference StoredActor => Header.Actor;
    public string AuthoredIntentSha256 => Operation.AuthoredIntentSha256;
    public Uuid CreateRequestId => Operation.CommandKind == nameof(CreateRiskDraft) ? Operation.LogicalId : Uuid.Empty;
}
