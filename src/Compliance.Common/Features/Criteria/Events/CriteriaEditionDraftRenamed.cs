using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-draft-renamed", 1)]
public sealed record CriteriaEditionDraftRenamed(Uuid EditionId, Uuid OperationId, string OldLabel, long OldLabelGeneration, string NewLabel, long NewLabelGeneration, Uuid ReservationEventId) : DomainEvent;
