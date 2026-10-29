using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

sealed record AdvisorFeedbackWorkItemEntry(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, Uuid WorkItemId, Uuid RecordId, string Kind, string Body,
    ActorReference Author, DateTimeOffset CreatedAt, bool HasResponded, bool IsResolved);
