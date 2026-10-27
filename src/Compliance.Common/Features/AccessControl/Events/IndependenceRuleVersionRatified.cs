using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>Records the IndependenceRuleVersionRatified domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.accesscontrol.independence-rule-version-ratified", 1)]
public sealed record IndependenceRuleVersionRatified(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
