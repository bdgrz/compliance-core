using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Commitments.Services;

public static class CommitmentResponses
{
    public const string Registration = "commitment-draft-registration.v1";
    public static async ValueTask<FirstResponseDescriptor> FreezeAsync(IImmutableContentStore content,
        CommitmentRules after, string kind, CancellationToken ct = default)
    {
        if (kind == nameof(CreateCommitmentDraft))
        {
            var body = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(), Registration,
                new CommitmentDraftRegistration(after.Id, after.Kind!, after.Identifier!, 1), ct).ConfigureAwait(false);
            return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, Registration, new StandardResponseBody(body));
        }
        var unit = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(), UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(unit));
    }
}
