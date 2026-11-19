using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-publication-aborted", 1)]
public sealed record CriteriaPublicationAborted(Uuid EditionId, Uuid DraftGeneration, Uuid PublicationAttemptId, long AttemptOrdinal, string WitnessIdentifier, Uuid WitnessIncarnationId, string Reason) : DomainEvent;
