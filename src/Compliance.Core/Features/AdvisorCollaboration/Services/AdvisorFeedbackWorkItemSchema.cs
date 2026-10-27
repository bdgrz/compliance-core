using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Services;

static class AdvisorFeedbackWorkItemSchema
{
    public static Uuid ThreadKey(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId) =>
        AdvisorFeedbackThread.IdentityOf(tenantId, programId, handoffId, feedbackId);

    public static Uuid WorkItemId(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId) =>
        WorkCandidate.IdFor(ThreadKey(tenantId, programId, handoffId, feedbackId), WorkSource.AdvisorFeedback);

    public static readonly KvDirectoryIndex<AdvisorFeedbackWorkItemEntry> ByProgram = new(
        "by_program", 1, static entry => [entry.ProgramId.ToString(), entry.WorkItemId.ToString()]);

    public static readonly KvDirectory<AdvisorFeedbackWorkItemEntry, Uuid> Feedback = new(
        "advisor_feedback", ComplianceCoreJsonContext.Default.AdvisorFeedbackWorkItemEntry,
        static entry => ThreadKey(entry.TenantId, entry.ProgramId, entry.HandoffId, entry.FeedbackId),
        static feedbackId => [feedbackId.ToString()], [ByProgram]);

    public static readonly KvDirectory<AdvisorFeedbackWorkItemProjectionState, Uuid> Threads =
        new("threads", ComplianceCoreJsonContext.Default.AdvisorFeedbackWorkItemProjectionState,
            static state => ThreadKey(state.TenantId, state.ProgramId, state.HandoffId, state.FeedbackId), static feedbackId => [feedbackId.ToString()], []);
}
