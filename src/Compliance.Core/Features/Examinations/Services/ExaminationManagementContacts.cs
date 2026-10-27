using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Services;

static class ExaminationManagementContacts
{
    const int MaximumContacts = 20;

    public static async ValueTask<RequestError?> ValidateAsync(IAggregateReader reader,
        Uuid tenantId, IReadOnlyList<Uuid> memberIds, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || memberIds is null || memberIds.Count > MaximumContacts ||
            memberIds.Any(static id => id == Uuid.Empty) ||
            memberIds.Distinct().Count() != memberIds.Count)
            return Invalid();

        foreach (var memberId in memberIds)
        {
            var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
                .ConfigureAwait(false);
            if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned ||
                member.Affiliation != "client_personnel")
                return Invalid();
        }

        return null;
    }

    static RequestError Invalid() => new(RequestErrorKind.Validation,
        "Management contacts must be unique, active client members of the examination tenant.");
}
