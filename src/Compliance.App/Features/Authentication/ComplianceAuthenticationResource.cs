using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>A trusted resource-server token issuer and audience pair.</summary>
public sealed record ComplianceAuthenticationResource(
    string Name,
    string Authority,
    string Audience,
    bool RequireHttpsMetadata);
