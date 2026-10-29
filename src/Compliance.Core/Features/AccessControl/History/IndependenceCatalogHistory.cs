using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.History;

public static class IndependenceCatalogHistory
{
    public static bool ProvesAbsent(ImmutableContentRef? head, long sequence,
        IReadOnlyList<IndependenceRatificationHistoryRead> history, Uuid ratificationId)
    {
        var reference = head;
        var expected = sequence;
        long? version = null;
        foreach (var entry in history)
        {
            var value = entry.Content;
            if (reference is null || reference != entry.Reference || !IndependenceCatalogContent.ValidRatification(value) || value.Sequence != expected ||
                version is { } later && value.Ratification.RuleVersion >= later || !IndependenceCatalogContent.Matches(reference, value, IndependenceCatalogContent.RatificationSchema))
                throw new ImmutableContentIntegrityException("The ratification history proof differs from its exact monotonic native cut.");
            if (value.Ratification.RatificationId == ratificationId)
                return false;
            reference = value.Previous;
            expected--;
            version = value.Ratification.RuleVersion;
        }
        if (reference is not null || expected != 0)
            throw new ImmutableContentIntegrityException("The historical ratification identity absence has not been proven.");
        return true;
    }
}
