using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Durable CAS store for the delivery process state of invitations.</summary>
public interface ITenantInvitationDeliveryStore
{
    /// <summary>Reads the record of one invitation, or null when no delivery began.</summary>
    Task<TenantInvitationDeliveryRecord?> ReadAsync(Uuid tenantId, Uuid invitationId, CancellationToken ct = default);

    /// <summary>Creates the record when absent and returns the stored record either way.</summary>
    Task<TenantInvitationDeliveryRecord> CreateIfAbsentAsync(TenantInvitationDeliveryRecord record,
        CancellationToken ct = default);

    /// <summary>Replaces the record when its stored version equals <paramref name="expectedVersion" />.</summary>
    /// <exception cref="Bdgrz.Compliance.Features.Processes.ProcessConcurrencyException">The stored version differs or the record is missing.</exception>
    Task<TenantInvitationDeliveryRecord> ReplaceAsync(TenantInvitationDeliveryRecord record, long expectedVersion,
        CancellationToken ct = default);
}
