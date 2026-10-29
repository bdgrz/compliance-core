using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>A denial-only wall over the canonical user's permanent practice commitment for the client.</summary>
public sealed class ClientManagementIndependenceGuard(IAggregateReader reader)
{
    public async ValueTask<bool> CanAuthorAsync(Uuid tenantId, Uuid canonicalUserId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || canonicalUserId == Uuid.Empty)
            return false;
        var user = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId,
            PracticeSubjectKind.CanonicalUser, canonicalUserId), ct).ConfigureAwait(false);
        return !user.Serves(PracticeIndependenceCommitment.Attest);
    }
}
