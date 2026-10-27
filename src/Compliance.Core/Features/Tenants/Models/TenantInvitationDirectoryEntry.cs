using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>
///     Only administrative display fields from the invitation stream: the latest invitation of a mailbox. The guest
///     scope stays a reference to its verified manifest and delivery state is read from the delivery process.
/// </summary>
public sealed record TenantInvitationDirectoryEntry(Uuid TenantId, Uuid InvitationId, string EmailAddress,
    string Affiliation, bool Administrator, string? BuiltInRole, DateTimeOffset ExpiresAt,
    Uuid InvitedBy, Uuid? AcceptedUserId, ImmutableContentRef? GuestProgramIdsManifest = null,
    DateOnly? GuestExpiresOn = null);
