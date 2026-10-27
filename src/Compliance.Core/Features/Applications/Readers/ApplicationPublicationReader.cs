using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Applications.Readers;

/// <summary>
///     Starts fixed-cut reads of declared applications. A response that can show import or bulk provisional effects
///     reads every application it composes at one captured global frontier, so a batch that publishes between two
///     targets is either visible to all of them or to none, and never leaks a later publication into a later page of
///     the same logical read.
/// </summary>
public sealed class ApplicationPublicationReader(IAuthoritativeSnapshotReader snapshots)
{
    /// <summary>Captures the global frontier every read of the returned cut shares.</summary>
    public async Task<ApplicationPublicationCut> BeginAsync(CancellationToken ct = default)
    {
        var capture = await snapshots.CaptureAsync(ct).ConfigureAwait(false);
        return new ApplicationPublicationCut(snapshots, capture.Cut);
    }

    /// <summary>
    ///     Continues a logical read at the watermark its first page captured. The anchor is captured again and must
    ///     not be older than that watermark; the selectors are then read at the original frontier, never at the
    ///     newer one, and a later publication stays invisible.
    /// </summary>
    public async Task<ApplicationPublicationCut> ResumeAsync(ulong watermark, CancellationToken ct = default)
    {
        var capture = await snapshots.CaptureAsync(ct).ConfigureAwait(false);
        if (capture.Cut.GlobalWatermarkExclusive < watermark)
            throw new SourceFactVerificationException("The anchor frontier is older than the continued cut.");
        return new ApplicationPublicationCut(snapshots, capture.Cut with { GlobalWatermarkExclusive = watermark });
    }

    /// <summary>Begins a read, or resumes the cut carried by a cursor.</summary>
    public Task<ApplicationPublicationCut> OpenAsync(ulong? watermark, CancellationToken ct = default) =>
        watermark is { } carried ? ResumeAsync(carried, ct) : BeginAsync(ct);
}
