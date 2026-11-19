using System.Globalization;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Programs.Events;

[Discriminator("bdgrz.program.targets.revised", 1)]
public sealed record ProgramTargetsRevised(Uuid TenantId, Uuid ProgramId, long Revision,
    ProgramPlan Plan, ImmutableContentRef PreviousContent, ImmutableContentRef Content, DateTimeOffset ChangedAt, OperationProvenance Operation,
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
