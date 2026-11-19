using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-initial-label-refused", 1)]
public sealed record CriteriaInitialLabelRefused(Uuid EditionId, Uuid OperationId, string Label, string Reason, Uuid RefusalEventId) : DomainEvent;
