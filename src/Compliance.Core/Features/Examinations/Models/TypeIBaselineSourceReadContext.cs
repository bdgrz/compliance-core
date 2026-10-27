using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Request authority and earlier source references available to one family reader.</summary>
public sealed record TypeIBaselineSourceReadContext(Uuid RequesterUserId,
    IReadOnlyList<TypeIBaselineSourceReference> PreviouslyCapturedSources)
{
    /// <summary>Optional server-frozen receipt cutoff for a close; the engagement date remains the effective cutoff.</summary>
    public DateTimeOffset? RecordedThrough { get; init; }
    public RecordedSourceReceiptCut? ReceiptCut { get; init; }
    public RecordedSourceReceiptCut RequireReceipts(Uuid tenant)
    {
        if (RecordedThrough is not { } through || ReceiptCut is not { } cut)
            throw new ImmutableContentUnavailableException("The exact closing source broker receipt selection is not configured.");
        if (cut.TenantId != tenant || cut.RecordedThrough != through)
            throw new ImmutableContentIntegrityException("The closing source broker receipt selection has another exact tenant or cutoff.");
        return cut;
    }
    public ValueTask<bool> IncludesAsync(EventStreamAddress stream, DomainEvent fact, CancellationToken ct) => RecordedThrough is null
        ? ValueTask.FromResult(true) : RequireReceipts(ReceiptCut?.TenantId ?? Uuid.Empty).IncludesAsync(stream, fact, ct);
}
