using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
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
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Features.Changes.Readers;

sealed class AccountableWorkItemOwnershipReader(
    IEnumerable<IAccountableWorkItemDirectoryReader> directories,
    WorkQueueReadConsistency consistency) : IChangeImpactWorkOwnershipReader
{
    readonly IAccountableWorkItemDirectoryReader[] _directories = directories
        .OrderBy(static directory => directory.ProjectorName, StringComparer.Ordinal).ToArray();

    public async ValueTask<Result> ConfirmOwnedByProgramAsync(Uuid tenantId, Uuid programId,
        IReadOnlyCollection<Uuid> workItemIds, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || workItemIds is null ||
            workItemIds.Any(static id => id == Uuid.Empty) ||
            workItemIds.Distinct().Count() != workItemIds.Count)
            return InvalidWorkScope();
        if (workItemIds.Count == 0)
            return Result.Success;

        var captured = await consistency.CaptureAsync(tenantId, ct).ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result.Failure(captured.Error);

        var expected = workItemIds.ToHashSet();
        var owners = new Dictionary<Uuid, WorkCandidate>();
        foreach (var directory in _directories)
        {
            var projected = await directory.LoadProgramAsync(tenantId, programId, ct)
                .ConfigureAwait(false);
            if (!projected.IsSuccess)
                return Result.Failure(projected.Error);

            foreach (var candidate in projected.Value.Where(candidate =>
                         expected.Contains(candidate.WorkItemId)))
            {
                if (!owners.TryAdd(candidate.WorkItemId, candidate))
                    return InvalidWorkScope();
            }
        }

        var caughtUp = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            captured.Value, ct).ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            return caughtUp;
        return owners.Count == expected.Count
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "One or more impact-review work items are not present in the current tenant and program work sources.",
                isTransient: true));
    }

    static Result InvalidWorkScope() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "Impact-review work ownership is ambiguous or outside the requested scope."));
}
