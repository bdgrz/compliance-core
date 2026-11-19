using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-drafted", 1)]
public sealed record CriteriaEditionDrafted(Uuid EditionId, Uuid DraftGeneration, Uuid OperationId,
    [property: JsonRequired] string Framework, string Label, long LabelGeneration, Uuid ConfirmationEventId) : DomainEvent;
