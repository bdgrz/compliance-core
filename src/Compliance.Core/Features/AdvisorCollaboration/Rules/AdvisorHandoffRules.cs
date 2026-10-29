using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Rules;

/// <summary>Request-level validation shared by the handoff and feedback commands.</summary>
static class AdvisorHandoffRules
{
    public static readonly IReadOnlySet<string> FeedbackKinds = new HashSet<string>(StringComparer.Ordinal)
        { "comment", "change_request", "validated" };

    public static bool ValidSelection(IReadOnlyList<AdvisorRecordVersion>? selection) =>
        selection is { Count: > 0 and <= AdvisorHandoff.MaximumSelectionCount } &&
        selection.All(static item => item is not null && item.HasValidIdentity) &&
        selection.Select(static item => (item.SourceFamily, item.RecordId, item.EntryId))
            .Distinct().Count() == selection.Count;

    public static bool ValidActor(ActorReference? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display);

    public static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    public static Result NotFound() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The advisor handoff was not found in the requested program."));

    public static Result Forbidden(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Forbidden, message));

    /// <summary>The handoff snapshot is created and belongs to the requested program.</summary>
    public static bool InProgram(AdvisorHandoff handoff, Uuid programId) =>
        handoff.IsCreated && handoff.ProgramId == programId;
}
