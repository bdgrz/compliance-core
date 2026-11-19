namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>An unknown or draft source that prevents a complete readiness verdict.</summary>
public sealed record ReadinessIncompleteInput(string Family, string Status, string Explanation);
