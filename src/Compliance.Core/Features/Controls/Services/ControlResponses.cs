using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Controls.Services;

public static class ControlResponses
{
    public const string Registration = "control-registration.v1";
    public const string Successor = "control-successor-registration.v1";
    public const string Retirement = "control-retirement-registration.v1";

    public static async ValueTask<FirstResponseDescriptor> FreezeAsync(IImmutableContentStore content,
        ControlRules after, string kind, CancellationToken ct = default)
    {
        var realm = after.TenantId.ToString();
        if (kind == nameof(CreateControlDraft))
            return await FreezeAsync(content, realm, Registration,
                new ControlRegistration(after.Id, after.Identifier, 1), ct).ConfigureAwait(false);
        if (kind == nameof(ProposeControlSuccessor))
            return await FreezeAsync(content, realm, Successor,
                new ControlSuccessorRegistration(after.Id, after.DraftVersionId, after.ApprovedVersion!.VersionId,
                    after.Revision), ct).ConfigureAwait(false);
        if (kind == nameof(ProposeControlRetirement))
            return await FreezeAsync(content, realm, Retirement,
                new ControlRetirementRegistration(after.Id, after.PendingRetirementId!.Value,
                    after.ApprovedVersion!.VersionId, after.Revision), ct).ConfigureAwait(false);
        return await FreezeAsync(content, realm, UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
    }

    static async ValueTask<FirstResponseDescriptor> FreezeAsync<T>(IImmutableContentStore content, string realm,
        string schema, T value, CancellationToken ct) where T : notnull
    {
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), schema, value, ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, schema,
            new StandardResponseBody(body));
    }
}
