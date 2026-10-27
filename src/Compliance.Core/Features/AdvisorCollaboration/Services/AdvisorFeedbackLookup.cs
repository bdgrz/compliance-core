using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Services;

/// <summary>
///     Tells a feedback thread that does not exist from one that the handoff admitted and that is not materialized
///     yet. The admission is the authority: an admitted thread is only waiting for its independent materialization.
/// </summary>
static class AdvisorFeedbackLookup
{
    /// <summary>Success when the thread exists; a retryable conflict when it is admitted; otherwise not found.</summary>
    public static async ValueTask<Result> RequireMaterializedAsync(AdvisorFeedbackReader threads, AdvisorFeedbackThread thread,
        Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId, CancellationToken ct)
    {
        if (thread.IsMaterialized)
            return Result.Success;
        var admitted = await threads.HasAdmissionAsync(tenantId, programId, handoffId, feedbackId, ct).ConfigureAwait(false);
        return admitted
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The advisor feedback is not materialized yet; retry shortly.", true))
            : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The advisor feedback was not found."));
    }
}
