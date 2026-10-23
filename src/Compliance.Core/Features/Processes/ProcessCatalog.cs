namespace Bdgrz.Compliance.Features.Processes;

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
