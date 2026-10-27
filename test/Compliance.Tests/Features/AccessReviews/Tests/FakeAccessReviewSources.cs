using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

sealed class FakeAccessReviewSources : IAccessReviewSources
{
    public IReadOnlyList<PersonView> People { get; set; } = [];
    public Dictionary<Uuid, IReadOnlyList<string>> Statuses { get; } = [];
    public Dictionary<Uuid, ServiceIdentityView> Identities { get; } = [];
    public Uuid? AccessOwner { get; set; }

    public ValueTask<Result<IReadOnlyList<PersonView>>> ListPeopleAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        ValueTask.FromResult(Result<IReadOnlyList<PersonView>>.Success(People));

    public ValueTask<Result<IReadOnlyList<string>>> RelationshipStatusesAsync(Uuid tenantId,
        Uuid personId, CancellationToken ct = default) =>
        ValueTask.FromResult(Result<IReadOnlyList<string>>.Success(
            Statuses.GetValueOrDefault(personId) ?? ["active"]));

    public ValueTask<Result<ServiceIdentityView>> GetServiceIdentityAsync(Uuid tenantId,
        Uuid serviceIdentityId, CancellationToken ct = default) =>
        ValueTask.FromResult(Identities.TryGetValue(serviceIdentityId, out var identity)
            ? Result<ServiceIdentityView>.Success(identity)
            : Result<ServiceIdentityView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service identity was not found.")));

    public ValueTask<Result<Uuid?>> AccessOwnerMemberAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default) =>
        ValueTask.FromResult(Result<Uuid?>.Success(AccessOwner));
}
