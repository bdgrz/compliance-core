using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Events;

[Discriminator("bdgrz.risk.draft.revised", 2)]
public sealed record RiskDraftRevised(RiskDraftAuthorityHeader Header, ImmutableContentRef Content,
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
    public Uuid? CommandId => Operation.LogicalId;
    public long? OriginalExpectedRevision => Operation.OriginalExpectedRevision is { } revision ? checked((long)revision) : null;
}
