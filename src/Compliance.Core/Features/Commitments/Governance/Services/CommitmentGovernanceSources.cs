using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Commitments.Governance.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Services;

/// <summary>Reads cited business authority, without consulting a directory for admission.</summary>
public sealed class CommitmentGovernanceSources(IAggregateReader reader, BoundaryReader boundaries, ProviderSource providers)
{
    public async ValueTask<Result<Uuid>> RequireAsync(Uuid tenant, Uuid program, string kind, CommitmentTerms terms,
        CancellationToken ct)
    {
        if (!Commitment.ValidTerms(kind, terms))
            return Failure(RequestErrorKind.Validation, "A typed commitment statement, citation, owner, categories and service are required.");
        var service = await reader.HydrateAsync(new ClientService(tenant, terms.ServiceId), ct).ConfigureAwait(false);
        var person = await reader.HydrateAsync(new Person(tenant, terms.OwnerPersonId), ct).ConfigureAwait(false);
        var boundary = await boundaries.ReadAsync(tenant, program, terms.BoundaryId, ct).ConfigureAwait(false);
        if (!service.IsActive || service.ProgramId != program || !person.IsCreated ||
            boundary.LatestApprovedVersion is not { Status: "approved" } approved ||
            approved.ProgramId != program || terms.Categories.Any(category => !approved.Content.TrustServicesCategories.Contains(category)))
            return Failure(RequestErrorKind.Conflict, "The current service, owner and approved boundary do not admit these commitment categories.");
        if (terms.ProviderId is { } provider)
        {
            var source = await providers.GetAsync(tenant, provider, ct).ConfigureAwait(false);
            if (source is null || !source.Content.Subservice)
                return Failure(RequestErrorKind.NotFound, "The subservice provider was not found in this tenant.");
        }
        return Result<Uuid>.Success(approved.VersionId);
    }
    static Result<Uuid> Failure(RequestErrorKind kind, string message) => Result<Uuid>.Failure(new RequestError(kind, message));
}
