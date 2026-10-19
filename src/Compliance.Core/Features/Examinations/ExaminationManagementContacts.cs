using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

static class ExaminationManagementContacts
{
    const int MaximumContacts = 20;

    public static async ValueTask<RequestError?> ValidateAsync(IAggregateReader reader,
        Uuid tenantId, IReadOnlyList<Uuid> memberIds, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || memberIds is null || memberIds.Count > MaximumContacts ||
            memberIds.Any(static id => id == Uuid.Empty) ||
            memberIds.Distinct().Count() != memberIds.Count)
            return Invalid();

        foreach (var memberId in memberIds)
        {
            var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
                .ConfigureAwait(false);
            if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned ||
                member.Affiliation != "client_personnel")
                return Invalid();
        }

        return null;
    }

    static RequestError Invalid() => new(RequestErrorKind.Validation,
        "Management contacts must be unique, active client members of the examination tenant.");
}
