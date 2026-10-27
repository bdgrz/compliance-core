using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>Registered process definitions, looked up by name or by root source fact kind.</summary>
public sealed class ProcessCatalog
{
    readonly Dictionary<string, ProcessDefinition> _byName = new(StringComparer.Ordinal);
    readonly Dictionary<string, ProcessDefinition> _byRoot = new(StringComparer.Ordinal);

    public ProcessCatalog(IEnumerable<ProcessDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions)
        {
            if (!_byName.TryAdd(definition.Name, definition))
                throw new InvalidOperationException($"Process {definition.Name} is defined twice.");
            foreach (var root in definition.RootSourceEvents)
            {
                if (!_byRoot.TryAdd(root, definition))
                    throw new InvalidOperationException($"Root fact {root} starts two processes.");
            }
        }
    }

    /// <summary>All registered definitions.</summary>
    public IReadOnlyCollection<ProcessDefinition> Definitions => _byName.Values;

    /// <summary>Finds a definition by process name.</summary>
    public ProcessDefinition Get(string name) => _byName.TryGetValue(name, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Process {name} is not registered.");

    /// <summary>Finds the process a root fact kind starts, if any.</summary>
    public ProcessDefinition? ForRoot(string rootEventName) => _byRoot.GetValueOrDefault(rootEventName);
}
