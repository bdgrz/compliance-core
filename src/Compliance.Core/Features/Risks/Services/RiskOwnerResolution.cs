using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Services;

/// <summary>Resolves risk scope and the platform member a workforce person is correlated with.</summary>
static class RiskOwnerResolution
{
    public static async ValueTask<(bool Exists, Uuid? MemberId)> ResolveAsync(
        IAggregateReader reader, Uuid tenantId, Uuid personId, CancellationToken ct)
    {
        var person = await reader.HydrateAsync(new Person(tenantId, personId), ct)
            .ConfigureAwait(false);
        return (person.IsCreated, person.CorrelatedUserId is { } userId
            ? RbacIds.Member(tenantId, userId)
            : null);
    }

    public static async ValueTask<Result> RequireRiskAsync(IAggregateReader reader,
        Uuid tenantId, Uuid programId, Uuid riskId, CancellationToken ct)
    {
        var risk = await reader.HydrateAsync(new RiskDraft(tenantId, riskId), ct)
            .ConfigureAwait(false);
        return risk.IsCreated && risk.ProgramId == programId
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The risk was not found."));
    }
}
