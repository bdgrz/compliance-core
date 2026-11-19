using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-published", 1)]
public sealed record CriteriaEditionPublished(Uuid EditionId, Uuid DraftGeneration, Uuid PublicationAttemptId, long AttemptOrdinal, string WitnessIdentifier, Uuid WitnessIncarnationId, string Label, long LabelGeneration, Uuid WitnessPinEventId, Uuid LabelConfirmationEventId, ActorAttribution PublishedBy) : DomainEvent;
