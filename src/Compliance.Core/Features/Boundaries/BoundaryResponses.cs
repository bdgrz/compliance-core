using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public static class BoundaryResponses
{
    public const string Registration = "boundary-registration.v1";
    public static async ValueTask<FirstResponseDescriptor> FreezeAsync(IImmutableContentStore content,
        BoundaryRules after, string kind, CancellationToken ct = default)
    {
        if (kind is nameof(CreateBoundary) or nameof(ProposeBoundarySuccessor))
        {
            var body = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(), Registration,
                new BoundaryRegistration(after.Id, after.DraftVersionId), ct).ConfigureAwait(false);
            return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, Registration, new StandardResponseBody(body));
        }
        var unit = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(), UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(unit));
    }
}
