using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit proof fixture for pure exact-scope policy tests; real resolver/handler tests verify stored approval sources.</summary>
static class ControlTestWaivers
{
    public static SeparationOfDutiesWaiverProof Proof(SeparationOfDutiesWaiver waiver)
    {
        var scope = waiver.Scope!;
        return new SeparationOfDutiesWaiverProof(waiver.TenantId, waiver.Id, waiver.BeneficiaryMemberId,
            waiver.BeneficiaryMemberId, scope.Action, scope.RecordType, scope.RecordId, scope.VersionId, scope.Revision,
            new SourceFactLocator(waiver.Stream, Uuid.CreateVersion4(), 1, 1, 1, 2, new string('a', 64)),
            waiver.ApprovedAt!.Value, waiver.ExpiresAt);
    }
}
