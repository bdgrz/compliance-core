using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Reports every actor as a member with one affiliation, or none as members.</summary>
sealed class FixedMembershipDirectory(bool member, string affiliation = "client_personnel",
    bool isSuspended = false, bool isDeprovisioned = false, DateOnly? membershipExpiresOn = null)
    : ITenantMembershipDirectoryReader
{
    public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
        CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(member
        ? new TenantMembershipView(userId, Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
            affiliation, IsSuspended: isSuspended, IsDeprovisioned: isDeprovisioned,
            MembershipExpiresOn: membershipExpiresOn)
        : null);

    public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
        CancellationToken ct = default) =>
        ValueTask.FromResult(member && !isSuspended && !isDeprovisioned &&
            (membershipExpiresOn is not { } expiresOn ||
             DateOnly.FromDateTime(DateTime.UtcNow) <= expiresOn));

    public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default) =>
        ValueTask.FromResult(new Page<TenantMembershipView>([], null));
}
