using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>
///     Lists one scope of the actor's work in M0-D15 order: due date (undated last), materiality,
///     creation, then work-item ID. Counts are computed from the same items at the same read.
/// </summary>
public sealed class ListWorkHandler(WorkQueueReader queue) : IRequestHandler<ListWork, WorkQueueView>
{
    public const int MaximumSearchLength = 200;

    public const string Mine = "mine";
    public const string Team = "team";
    public const string Unassigned = "unassigned";
    public const string Escalated = "escalated";
    public const string All = "all";

    public async ValueTask<Result<WorkQueueView>> HandleAsync(IRequestContext<ListWork> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var scope = request.Scope ?? Mine;
        if (scope is not (Mine or Team or Unassigned or Escalated or All))
            return Result<WorkQueueView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The scope must be mine, team, unassigned, escalated, or all."));
        if (request.HorizonDays is < 0 or > ControlCadenceSchedule.MaximumDueWithinDays)
            return Result<WorkQueueView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The horizon must be between 0 and 365 days."));
        if (request.Search is { Length: > MaximumSearchLength })
            return Result<WorkQueueView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The search must be at most 200 characters."));
        var search = request.Search?.Trim();
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var read = await queue.ReadAsync(request.TenantId, request.ProgramId, actor,
            request.HorizonDays ?? WorkQueueReader.DefaultHorizonDays, ct).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<WorkQueueView>.Failure(read.Error);
        var snapshot = read.Value;
        var items = snapshot.Entries.Where(entry => scope switch
            {
                Mine => entry.Item.AssigneeMemberId == actor.MemberId,
                Team => entry.ActorInTeam,
                Unassigned => entry.Item.AssigneeMemberId is null && entry.ActorEligible,
                Escalated => entry.Item.Escalated,
                _ => true,
            })
            .Select(static entry => entry.Item)
            .Where(item => string.IsNullOrEmpty(search) ||
                item.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Reason.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Kind.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Result<WorkQueueView>.Success(new WorkQueueView(scope, snapshot.Today,
            new WorkCountsView(items.Length, items.Count(static item => item.Overdue),
                items.Count(item => item.DueOn == snapshot.Today),
                items.Count(static item => item.Escalated)), items));
    }
}
