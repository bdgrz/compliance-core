namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Indicates that membership facts cannot form a valid member history.</summary>
public sealed class MemberHistoryIntegrityException(string message) : InvalidOperationException(message);
