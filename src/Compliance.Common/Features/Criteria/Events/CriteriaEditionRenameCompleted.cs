using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-rename-completed", 1)]
public sealed record CriteriaEditionRenameCompleted(Uuid EditionId, Uuid OperationId, string Label, long LabelGeneration, Uuid ConfirmationEventId) : DomainEvent;
