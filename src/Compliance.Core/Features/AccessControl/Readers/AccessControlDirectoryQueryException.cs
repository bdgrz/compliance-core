namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>The continuation does not belong to the requested access directory query.</summary>
sealed class AccessControlDirectoryQueryException() : Exception("The access directory cursor is invalid.");
