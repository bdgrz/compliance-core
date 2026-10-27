using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>Development-only identity settings used by the registration handler.</summary>
public sealed record DeveloperUserRegistration(bool Enabled)
{
    public const string Provider = "bdgrz-development";

    public static Uuid IdentifierNamespaceId { get; } =
        Uuid.Parse("8b53bb9d-09b9-5f2b-934c-2f04296d64ee", CultureInfo.InvariantCulture);
}
