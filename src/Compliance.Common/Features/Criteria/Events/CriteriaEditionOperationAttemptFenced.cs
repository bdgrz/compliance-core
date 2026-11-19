using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-operation-attempt-fenced", 1)]
public sealed record CriteriaEditionOperationAttemptFenced(Uuid EditionId, Uuid OperationId, long ExecutionGeneration, ulong ExpectedPosition, string IntentSha256) : DomainEvent;
