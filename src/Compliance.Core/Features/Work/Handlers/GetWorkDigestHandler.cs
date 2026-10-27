using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>
///     The actor's weekly digest content for this week: assigned items that are overdue or due in
///     the next seven days, read from current source state. Email delivery is not part of R2-11c's
///     in-product scope; the email preference is reported so a later sender can honor it.
/// </summary>
public sealed class GetWorkDigestHandler(WorkQueueReader queue, IAggregateReader reader)
    : IRequestHandler<GetWorkDigest, WorkDigestView>
{
    const int DueSoonDays = 7;

    public async ValueTask<Result<WorkDigestView>> HandleAsync(IRequestContext<GetWorkDigest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var read = await queue.ReadAsync(request.TenantId, request.ProgramId, actor,
            DueSoonDays, ct).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<WorkDigestView>.Failure(read.Error);
        var snapshot = read.Value;
        var today = snapshot.Today;
        var mine = snapshot.Entries.Select(static entry => entry.Item)
            .Where(item => item.AssigneeMemberId == actor.MemberId && item.DueOn is not null)
            .ToArray();
        var weekOf = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var preference = await reader.HydrateAsync(new WorkDigestPreference(request.TenantId,
            actor.MemberId), ct).ConfigureAwait(false);
        return Result<WorkDigestView>.Success(new WorkDigestView(
            Uuid.CreateVersion5(actor.MemberId, string.Create(CultureInfo.InvariantCulture,
                $"digest:{request.ProgramId}:{weekOf:yyyy-MM-dd}")),
            weekOf, preference.Read().EmailDigestEnabled,
            mine.Where(item => item.DueOn < today).ToArray(),
            mine.Where(item => item.DueOn >= today && item.DueOn <= today.AddDays(DueSoonDays))
                .ToArray()));
    }
}
