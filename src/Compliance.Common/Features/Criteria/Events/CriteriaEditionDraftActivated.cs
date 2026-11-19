using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-draft-activated", 1)]
public sealed record CriteriaEditionDraftActivated(Uuid EditionId, Uuid DraftGeneration, Uuid OperationId,
    [property: JsonRequired] string Framework, string Label, long LabelGeneration, Uuid ReservationEventId) : DomainEvent;
