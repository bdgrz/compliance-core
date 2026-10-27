using System.Globalization;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.retired", 2)]
public sealed record ClientServiceRetired(Uuid TenantId, Uuid ServiceId, Uuid ProgramId, long Revision,
    ImmutableContentRef Content, ImmutableContentRef Retirement, DateTimeOffset ChangedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, ulong NativeSequence)
    : DomainEvent, IProductClientServiceFact
{
    [JsonIgnore]
    public ActorReference Actor => Operation.Actor;
    [JsonIgnore]
    public Uuid ActorMemberId => Uuid.TryParse(Actor.Id, CultureInfo.InvariantCulture, out var member) ? member : Uuid.Empty;
    [JsonIgnore]
    public string ActorDisplay => Actor.Display;
}
