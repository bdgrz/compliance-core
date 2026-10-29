using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Versioning.Models;
namespace Bdgrz.Compliance.Features.Readiness.Services;

/// <summary>Reconstructs a control interval from lifecycle decisions known at the assessment instant.</summary>
static class ReadinessControlSelection
{
    public static ControlVersionView? EffectiveVersionAt(ControlReadSnapshot control, DateTimeOffset asOf)
    {
        var date = DateOnly.FromDateTime(asOf.UtcDateTime);
        var approvals = control.ReadApprovedVersionHistory()
            .Where(version => version.ApprovedAt <= asOf).ToArray();
        var retirements = control.ReadRetirementHistory()
            .Where(retirement => retirement.DecidedAt <= asOf).ToArray();
        foreach (var version in approvals.Reverse())
        {
            var successor = approvals.SingleOrDefault(candidate =>
                candidate.PredecessorVersionId == version.VersionId);
            var retirement = retirements.SingleOrDefault(candidate => candidate.VersionId == version.VersionId);
            var until = retirement?.EffectiveUntil ?? successor?.EffectiveFrom;
            if (new EffectiveInterval(version.EffectiveFrom, until).Contains(date))
                return version with
                {
                    EffectiveUntil = until,
                    Status = retirement is not null ? "retired" : successor is not null ? "superseded" : "approved",
                };
        }
        return null;
    }
}
