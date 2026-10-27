using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     Frozen first response of a team assignment or removal. A direct non-administrator source mutation is
///     <see cref="AdministratorTransitionResponseState.Completed" />, and exact retries repeat the response.
/// </summary>
public sealed record AdministratorTransitionResponse(Uuid OperationId, AdministratorTransitionResponseState State,
    string? StatusUrl, DateTimeOffset AcceptedAt)
{
    /// <summary>Schema of the frozen response content.</summary>
    public const string Schema = "administrator-transition-response.v1";
}
