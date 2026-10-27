using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

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
