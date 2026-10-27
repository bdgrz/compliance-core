using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Policies.Rules;

static class PolicyWaiverProof
{
    public static bool Allows(SeparationOfDutiesWaiverProof proof, Uuid tenant,
        SeparationOfDutiesWaiverScope scope, Uuid actor, DateTimeOffset at) =>
        proof.TenantId == tenant && proof.WaiverId != Uuid.Empty && proof.SubjectId == actor && proof.ActorId == actor &&
        proof.Action == scope.Action && proof.TargetType == scope.RecordType && proof.TargetId == scope.RecordId &&
        proof.TargetVersionId == scope.VersionId && proof.TargetRevision == scope.Revision &&
        proof.ApprovalFact.Source == new EventStreamAddress(tenant.ToString(), "separation-of-duties-waivers", proof.WaiverId.ToString()) &&
        proof.ApprovalFact.EventId != Uuid.Empty && proof.ApprovalFact.ResourcePosition == 1 && proof.ApprovalFact.AggregateVersion == 2 &&
        proof.ApprovalFact.PayloadSha256 is { Length: 64 } hash && hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        at >= proof.ValidFrom && at < proof.ValidUntil;
}
