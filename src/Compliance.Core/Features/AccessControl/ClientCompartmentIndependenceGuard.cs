using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A denial-only compartment wall over the canonical user's permanent practice commitment; ordinary grants remain mandatory.</summary>
public sealed class ClientCompartmentIndependenceGuard(IAggregateReader reader)
{
    public async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid canonicalUserId,
        RecordCompartment compartment, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || canonicalUserId == Uuid.Empty || !Enum.IsDefined(compartment))
            return false;
        var user = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId,
            PracticeSubjectKind.CanonicalUser, canonicalUserId), ct).ConfigureAwait(false);
        return !IndependenceCompartments.IsBlockedByWall(tenantId, user.Assignments(), compartment);
    }
}
