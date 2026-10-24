using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.reviewed", 1)]
public sealed record PolicyReviewed(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, Uuid DecisionId, string Outcome, string Rationale, ActorReference Actor,
    Uuid ActorMemberId, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId)
    : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationProvenance? Operation { get; init; }
}
