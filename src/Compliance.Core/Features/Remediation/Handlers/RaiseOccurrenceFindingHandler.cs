using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Reactors;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>
///     Raises the finding for a failed or skipped attestation or a reviewer's requested action.
///     The control owner (or the recorder when the owner is not a member) owns it, due in 30 days
///     at medium severity as a product default the compliance lead may revise. A requested action
///     also becomes the finding's corrective action, admitted atomically with the raise. Replays are idempotent by
///     finding ID.
/// </summary>
public sealed class RaiseOccurrenceFindingHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ControlOperationsReader operatingReader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RaiseOccurrenceFinding>
{
    public const int DefaultDueDays = RoutedFinding.DefaultDueDays;
    public const string DefaultSeverity = RoutedFinding.DefaultSeverity;
    public const string ProcessId = "reactor:" + ControlOccurrenceFindingReactor.WorkloadName;

    public async ValueTask<Result> HandleAsync(IRequestContext<RaiseOccurrenceFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        var actor = ActorReference.ForSystemProcess(ProcessId, ControlOccurrenceFindingReactor.WorkloadName);
        async ValueTask<Result<RoutedFindingSource>> PrepareSourceAsync(CancellationToken token)
        {
            var snapshot = await operatingReader.ReadOccurrenceAsync(request.TenantId, request.ProgramId, request.ControlId,
                request.OccurrenceId, token).ConfigureAwait(false);
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (snapshot.ReadOccurrence(request.ControlId, request.OccurrenceId,
                    new Dictionary<Uuid, DateOnly?>(), today) is not { Revision: > 0 } occurrence ||
                occurrence.Attestations.Count == 0)
                return Result<RoutedFindingSource>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The control occurrence was not found."));
            var owner = snapshot.CurrentPlan(request.ControlId)?.Owner is { Kind: "member" } member
                ? member.Id
                : occurrence.Reviews.Count > 0
                    ? occurrence.Reviews[^1].ReviewerMemberId
                    : occurrence.Attestations[^1].RecorderMemberId;
            var requestedAction = request.SourceKind == "occurrence_review";
            return Result<RoutedFindingSource>.Success(new RoutedFindingSource(
                new FindingSource(request.SourceKind, request.OccurrenceId, request.SourceVersion, request.SourceText),
                requestedAction ? "Requested action from control review" : "Control occurrence exception",
                $"Control {request.ControlId}, occurrence {request.OccurrenceId}", owner,
                [new FindingLink("control", request.ControlId.ToString()),
                    new FindingLink("control_occurrence", request.OccurrenceId.ToString())],
                requestedAction ? request.SourceText : null));
        }
        return await RoutedFinding.RaiseFromSourceAsync(operations, reader, content, context, "RaiseOccurrenceFinding",
            request, request.TenantId, request.ProgramId, request.FindingId, actor, now, PrepareSourceAsync, ct)
            .ConfigureAwait(false);
    }
}
