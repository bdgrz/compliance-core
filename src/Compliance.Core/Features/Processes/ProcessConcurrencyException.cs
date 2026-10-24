namespace Bdgrz.Compliance.Features.Processes;

/// <summary>The record changed under the worker; the caller re-reads and decides again.</summary>
public sealed class ProcessConcurrencyException(string message) : Exception(message);
