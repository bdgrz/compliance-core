namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Qualifies readiness of the criteria authoring provider and durable-worker graph.</summary>
/// <remarks>
/// True requires independently established source, admission and conditional-write guarantees,
/// plus the durable decision, reconciliation and projection workers needed to settle the
/// submitted work. Registration, an empty queue or a quiet source is not qualification.
/// Unknown or incomplete evidence returns false. When no gate is configured, the API may
/// accept work as pending; this does not assert that a worker is currently running.
/// This check is not admission and does not reserve worker availability or fence an append:
/// deployment activation and shutdown must coordinate in-flight operations independently.
/// </remarks>
public interface ICriteriaAuthoringReadiness
{
    /// <summary>Returns true only while the full authoring graph is independently qualified.</summary>
    ValueTask<bool> IsReadyAsync(CancellationToken ct = default);
}
