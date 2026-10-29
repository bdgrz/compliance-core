using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Tenants.Processes;

/// <summary>Durable CAS store for the delivery process state of invitations.</summary>
public interface ITenantInvitationDeliveryStore
{
    /// <summary>Reads the record of one invitation, or null when no delivery began.</summary>
    Task<TenantInvitationDeliveryRecord?> ReadAsync(Uuid tenantId, Uuid invitationId, CancellationToken ct = default);

    /// <summary>Creates the record when absent and returns the stored record either way.</summary>
    Task<TenantInvitationDeliveryRecord> CreateIfAbsentAsync(TenantInvitationDeliveryRecord record,
        CancellationToken ct = default);

    /// <summary>Replaces the record when its stored version equals <paramref name="expectedVersion" />.</summary>
    /// <exception cref="Bdgrz.Compliance.Features.Processes.Processes.ProcessConcurrencyException">The stored version differs or the record is missing.</exception>
    Task<TenantInvitationDeliveryRecord> ReplaceAsync(TenantInvitationDeliveryRecord record, long expectedVersion,
        CancellationToken ct = default);
}
