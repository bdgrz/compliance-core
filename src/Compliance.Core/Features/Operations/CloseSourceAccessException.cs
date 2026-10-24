namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Complete close capture cannot silently omit a restricted source the current actor cannot read.</summary>
public sealed class CloseSourceAccessException : Exception
{
    public CloseSourceAccessException() : base("The actor cannot capture every required restricted close source.") { }
}
