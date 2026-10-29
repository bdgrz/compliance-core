using System.Globalization;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>The exact native program acceptance; authored value bytes remain in verified immutable content.</summary>
[Discriminator("bdgrz.program.criteria.selected", 2)]
public sealed record ProgramCriteriaEditionSelected(Uuid TenantId, Uuid ProgramId, long Revision, Uuid EditionId,
    ImmutableContentRef Content, DateTimeOffset ChangedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, ulong NativeSequence, int ChangeCount)
    : DomainEvent, IProgramFact
{
    [JsonIgnore]
    public ActorReference Actor => Operation.Actor;
    [JsonIgnore]
    public Uuid ActorMemberId => Uuid.TryParse(Actor.Id, CultureInfo.InvariantCulture, out var member) ? member : Uuid.Empty;
    [JsonIgnore]
    public string ActorDisplay => Actor.Display;
}
