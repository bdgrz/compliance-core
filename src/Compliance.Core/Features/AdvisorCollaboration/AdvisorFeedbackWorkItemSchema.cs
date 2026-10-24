using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

static class AdvisorFeedbackWorkItemSchema
{
    public static readonly KvDirectoryIndex<AdvisorFeedbackWorkItemEntry> ByProgram = new(
        "by_program", 1, static entry => [entry.ProgramId.ToString(), entry.WorkItemId.ToString()]);

    public static readonly KvDirectory<AdvisorFeedbackWorkItemEntry, Uuid> Feedback = new(
        "advisor_feedback", ComplianceCoreJsonContext.Default.AdvisorFeedbackWorkItemEntry,
        static entry => entry.FeedbackId,
        static feedbackId => [feedbackId.ToString()], [ByProgram]);

    public static readonly KvDirectory<AdvisorFeedbackWorkItemProjectionState, Uuid> Threads =
        new("threads", ComplianceCoreJsonContext.Default.AdvisorFeedbackWorkItemProjectionState,
            static state => state.FeedbackId, static feedbackId => [feedbackId.ToString()], []);
}
