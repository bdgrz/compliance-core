using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Policies.Handlers;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>
///     The authored fields of a credential rotation that its intent digest covers. The secret-store pointer is
///     represented by the digest of its protected content, never by the pointer.
/// </summary>
public sealed record RotateTenantIdentityProviderCredentialsAuthoredIntent(Uuid TenantId, Uuid ProviderConnectionId,
    string CredentialSha256, string RotationReference, long ExpectedRevision);
