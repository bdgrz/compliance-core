using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.SourceFacts.Services;

/// <summary>Creates one immutable request-relative broker receipt selection shared by all closing source families.</summary>
public sealed class RecordedSourceReceiptCutFactory(IDomainEventReader events, INativeEventReceiptReader receipts)
{
    public RecordedSourceReceiptCut Create(Uuid tenant, DateTimeOffset through) => new(tenant, through, events, receipts);
}
