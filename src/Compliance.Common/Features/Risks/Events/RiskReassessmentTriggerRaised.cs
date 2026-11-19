using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Risks.Events;

[Discriminator("bdgrz.risk.program_trigger.raised", 1)]
public sealed record RiskReassessmentTriggerRaised(Uuid TenantId, Uuid ProgramId, Uuid TriggerId,
    Uuid CommandId, string Kind, string Summary, SourceFactLocator? Source, string AuthoredIntentSha256,
    ActorReference RaisedBy, DateTimeOffset RaisedAt) : DomainEvent;
