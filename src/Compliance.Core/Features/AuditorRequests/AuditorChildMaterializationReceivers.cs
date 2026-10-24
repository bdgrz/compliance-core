using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Shared construction of the auditor request receivers of <c>IndependentChildMaterialization</c>. A record belongs to
///     a receiver only when its source is a request stream and its frozen intent names that child kind, so the other
///     kinds listed by the shared definition are never built from an auditor admission.
/// </summary>
static class AuditorChildMaterializationReceivers
{
    /// <summary>The frozen admission of a record when it is a child admission of <paramref name="kind" />, else null.</summary>
    public static async ValueTask<ChildAdmissionIntent?> AdmissionAsync(ProcessStep step, AdmittedChildKind kind,
        IServiceProvider services, CancellationToken ct)
    {
        var record = step.Record;
        if (step.Target is null || record.Source.Stream.Area != AuditorRequest.Area ||
            record.Intent.Schema != ChildAdmissionIntent.Schema)
            return null;
        var tenant = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        try
        {
            var intent = await AuditorContent.ReadAsync<ChildAdmissionIntent>(
                services.GetRequiredService<IImmutableContentStore>(), tenant, record.Intent, ct).ConfigureAwait(false);
            return intent.ChildKind == kind ? intent : null;
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactVerificationException($"The frozen admission cannot be verified yet: {exception.Message}");
        }
    }

    /// <summary>Reads the authored content of an admission, or waits when it cannot be verified yet.</summary>
    public static async ValueTask<T> AuthoredAsync<T>(ProcessStep step, ChildAdmissionIntent intent,
        IServiceProvider services, CancellationToken ct) where T : notnull
    {
        try
        {
            return await AuditorContent.ReadAsync<T>(services.GetRequiredService<IImmutableContentStore>(),
                Uuid.Parse(step.Record.Key.Realm, CultureInfo.InvariantCulture), intent.CanonicalAuthoredContent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactVerificationException($"The frozen admission cannot be verified yet: {exception.Message}");
        }
    }
}
