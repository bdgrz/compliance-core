namespace Bdgrz.Compliance.Features.Workforce.Readers;

/// <summary>A continuation does not belong to the requested workforce query.</summary>
sealed class WorkforceDirectoryQueryException() : Exception("The workforce cursor is invalid.");
