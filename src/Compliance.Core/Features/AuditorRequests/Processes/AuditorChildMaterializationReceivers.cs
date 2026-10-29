using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Processes;

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
