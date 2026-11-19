namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Indicates that the criteria source cannot furnish the exact ordering evidence requested.</summary>
public sealed class CriteriaSourceUnavailableException : Exception
{
    /// <summary>Creates an unavailable-source error.</summary>
    public CriteriaSourceUnavailableException(string message) : base(message)
    {
    }
}
