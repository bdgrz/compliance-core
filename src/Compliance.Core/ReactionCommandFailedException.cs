using Cntryl.Portia;

namespace Bdgrz.Compliance;

/// <summary>
/// Indicates that a command dispatched by a reactor returned an expected application failure.
/// </summary>
/// <param name="error">The expected request failure returned by the command.</param>
public sealed class ReactionCommandFailedException(RequestError error)
    : Exception("A command dispatched by a reactor did not succeed.")
{
    /// <summary>Gets the expected request failure returned by the command.</summary>
    public RequestError Error { get; } = error ?? throw new ArgumentNullException(nameof(error));
}
