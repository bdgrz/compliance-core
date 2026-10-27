using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>Deterministic identities derived from an invitation id.</summary>
public static class TenantInvitationIds
{
    static readonly Uuid AcceptanceNamespace = Uuid.Parse("5d0b3a52-0f0f-5c57-9a63-6d1f0ce1f7a4",
        CultureInfo.InvariantCulture);

    /// <summary>
    ///     The logical operation id of accepting an invitation. The invitation id itself is the logical id of its
    ///     creation, and one owner stream never accepts one logical id twice, so the acceptance has its own id.
    /// </summary>
    public static Uuid AcceptanceOf(Uuid invitationId) =>
        Uuid.CreateVersion5(AcceptanceNamespace, "acceptance\n" + invitationId.ToString());

    /// <summary>
    ///     The invitation id of the first administrator a tenant registration designates. A tenant is registered once,
    ///     so the registration reaction and any caller that must find the invitation derive the same id.
    /// </summary>
    public static Uuid FirstAdministratorOf(Uuid tenantId) =>
        Uuid.CreateVersion5(AcceptanceNamespace, "first-administrator\n" + tenantId.ToString());
}
