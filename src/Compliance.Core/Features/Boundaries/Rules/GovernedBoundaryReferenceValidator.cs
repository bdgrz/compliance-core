using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Boundaries.Rules;

public sealed class GovernedBoundaryReferenceValidator(IClientServiceActivity services,
    IApplicationInventoryActivity applications, ITechnologyInventoryActivity? inventory = null)
    : IBoundaryReferenceValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, Uuid programId, BoundaryContent content,
        CancellationToken ct = default)
    {
        if (content?.Entries is null)
            return Result.Success;
        foreach (var entry in content.Entries.Where(static entry => entry is { Unresolved: false }))
        {
            if (entry.GovernedRecordId is not { } id)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "A governed scope reference requires a record ID."));
            switch (entry.SubjectType)
            {
                case "service":
                    if (!await services.IsActiveAsync(tenantId, programId, id, ct)
                            .ConfigureAwait(false))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed service reference is not active in this program."));
                    break;
                case "application":
                    if (!await applications.IsDeclaredAsync(tenantId, id, ct)
                            .ConfigureAwait(false))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed application reference is unavailable in this tenant."));
                    break;
                case "system_instance":
                    var state = await applications.GetInstanceStateAsync(tenantId, id, ct)
                        .ConfigureAwait(false);
                    if (state == SystemInstanceReferenceState.Pending)
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed system instance reference has not finished projecting. Retry the command.",
                            isTransient: true));
                    if (state != SystemInstanceReferenceState.Declared)
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed system instance reference is unavailable in this tenant. Retry after projection or correct the record ID."));
                    break;
                case "component" or "information" or "data_flow" or "location" or "process"
                    when inventory is not null:
                    if (!await inventory.IsActiveAsync(tenantId, entry.SubjectType, id, ct)
                            .ConfigureAwait(false))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed technology inventory reference is not active in this tenant."));
                    break;
                default:
                    return Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "A governed scope reference requires its owning inventory to validate the record."));
            }
        }
        return Result.Success;
    }
}
