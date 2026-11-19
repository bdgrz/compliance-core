using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Rules;

/// <summary>Checks the exact operation binding of a trusted access-authority decision.</summary>
static class ProgramAdmissionEvidence
{
    public static bool MatchesNew(AccessAdmissionEvidence? evidence, Uuid tenantId, Uuid operationId,
        EventStreamAddress target, OperationProvenance operation, ulong? creationGeneration = null) =>
        AccessAdmissionEvidenceBinding.IsCurrentFormat(evidence) &&
        Matches(evidence, tenantId, operationId, target, operation, creationGeneration);

    public static bool Matches(AccessAdmissionEvidence? evidence, Uuid tenantId, Uuid operationId,
        EventStreamAddress target, OperationProvenance operation, ulong? creationGeneration = null)
    {
        if (evidence is null ||
            !AccessAdmissionEvidenceBinding.BindsProgramCreation(evidence, evidence.UserId,
                new ActorAttribution(evidence.ActorSubject, evidence.ActorIssuer), operationId,
                target.ToString(), creationGeneration is { } generation
                    ? CreationDigest(operation.AuthoredIntentSha256, generation)
                    : operation.AuthoredIntentSha256) ||
            evidence.AdmittedAt != operation.AcceptedAt || evidence.ActorIssuer != "bdgrz" ||
            !Uuid.TryParse(evidence.ActorSubject, CultureInfo.InvariantCulture, out var subject) || subject != evidence.UserId ||
            !Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var member) ||
            member != RbacIds.Member(tenantId, evidence.UserId))
            return false;
        return true;
    }

    public static string CreationDigest(string authoredIntentDigest, ulong generation)
    {
        var payload = string.Concat("bdgrz.program-creation-admission.v1\n", authoredIntentDigest, "\n",
            generation.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
