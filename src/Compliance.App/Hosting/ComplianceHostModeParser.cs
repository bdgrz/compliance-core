using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Incidents.Commands;
namespace Bdgrz.Compliance.Hosting;

/// <summary>Parses and describes the process-level compliance host mode.</summary>
public static class ComplianceHostModeParser
{
    public static ComplianceHostMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ComplianceHostMode.Standalone;
        }

        if (Enum.TryParse<ComplianceHostMode>(value.Trim(), ignoreCase: true, out var mode) &&
            Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new InvalidOperationException(
            $"COMPLIANCE_HOST_MODE must be one of standalone, api, or worker; received '{value}'.");
    }

    public static bool RunsApi(this ComplianceHostMode mode) =>
        mode is ComplianceHostMode.Standalone or ComplianceHostMode.Api;

    public static bool RunsWorkers(this ComplianceHostMode mode) =>
        mode is ComplianceHostMode.Standalone or ComplianceHostMode.Worker;
}
