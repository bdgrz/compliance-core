using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

static class BoundaryWaiverProof
{
    public static bool Allows(SeparationOfDutiesWaiverProof proof, Uuid tenant, SeparationOfDutiesWaiverScope scope,
        Uuid actor, DateTimeOffset at) => proof.TenantId == tenant && proof.WaiverId != Uuid.Empty && proof.SubjectId == actor &&
        proof.ActorId == actor && proof.Action == scope.Action && proof.TargetType == scope.RecordType && proof.TargetId == scope.RecordId &&
        proof.TargetVersionId == scope.VersionId && proof.TargetRevision == scope.Revision &&
        proof.ApprovalFact.Source == new EventStreamAddress(tenant.ToString(), "separation-of-duties-waivers", proof.WaiverId.ToString()) &&
        proof.ApprovalFact.EventId != Uuid.Empty && proof.ApprovalFact.ResourcePosition == 1 && proof.ApprovalFact.AggregateVersion == 2 &&
        proof.ApprovalFact.PayloadSha256 is { Length: 64 } sha && sha.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        at >= proof.ValidFrom && at < proof.ValidUntil;
}
