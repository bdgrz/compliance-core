using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>
///     A validated, normalized OIDC connection definition. <paramref name="CredentialReference" /> is the secret-store
///     pointer; it is handled only to be frozen as protected content and to be compared by digest.
/// </summary>
public sealed record TenantIdentityProviderConfiguration(string Issuer, string ClientId,
    IReadOnlyList<string> AllowedAudiences, string CredentialReference);
