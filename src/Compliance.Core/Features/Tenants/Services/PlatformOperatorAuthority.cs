using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>Configuration used once to seed the event-sourced operator roster.</summary>
public sealed class PlatformOperatorAuthority
{
    readonly Uuid[] _userIds;

    public PlatformOperatorAuthority(IEnumerable<Uuid> userIds, bool developerAuthentication = false)
    {
        _userIds = [.. userIds.Distinct()];
        DeveloperAuthentication = developerAuthentication;
    }

    public bool DeveloperAuthentication { get; }
    public IReadOnlyList<Uuid> BootstrapUserIds => _userIds;

    public static PlatformOperatorAuthority FromConfiguration(IConfiguration configuration,
        bool developerAuthentication)
    {
        var userIds = new List<Uuid>();
        foreach (var value in configuration.GetSection("PlatformOperators:UserIds").GetChildren())
        {
            if (!Uuid.TryParse(value.Value, CultureInfo.InvariantCulture, out var userId) || userId == Uuid.Empty)
                throw new InvalidOperationException("PlatformOperators:UserIds must contain nonempty UUIDs.");
            userIds.Add(userId);
        }

        return new PlatformOperatorAuthority(userIds, developerAuthentication);
    }
}
