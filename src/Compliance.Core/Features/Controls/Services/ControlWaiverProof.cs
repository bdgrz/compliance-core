using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>Pure comparison of an already resolved immutable waiver proof with one exact local decision.</summary>
static class ControlWaiverProof
{
    public static bool Allows(SeparationOfDutiesWaiverProof proof, Uuid tenant,
        SeparationOfDutiesWaiverScope scope, Uuid actor, DateTimeOffset at) =>
        proof.TenantId == tenant && proof.WaiverId != Uuid.Empty && proof.SubjectId == actor && proof.ActorId == actor &&
        proof.Action == scope.Action && proof.TargetType == scope.RecordType && proof.TargetId == scope.RecordId &&
        proof.TargetVersionId == scope.VersionId && proof.TargetRevision == scope.Revision &&
        proof.ApprovalFact.Source == new EventStreamAddress(tenant.ToString(), "separation-of-duties-waivers", proof.WaiverId.ToString()) &&
        at >= proof.ValidFrom && at < proof.ValidUntil;
}
