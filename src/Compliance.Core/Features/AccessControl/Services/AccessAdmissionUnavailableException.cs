namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Current source-backed authorization could not be established.</summary>
public sealed class AccessAdmissionUnavailableException : IOException
{
    /// <summary>Creates an unavailable-admission error.</summary>
    public AccessAdmissionUnavailableException() : base("Current source-backed admission is unavailable.")
    {
    }
}
