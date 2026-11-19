using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-rename-refused", 1)]
public sealed record CriteriaEditionRenameRefused(Uuid EditionId, Uuid OperationId, string Reason, Uuid RefusalEventId) : DomainEvent;
