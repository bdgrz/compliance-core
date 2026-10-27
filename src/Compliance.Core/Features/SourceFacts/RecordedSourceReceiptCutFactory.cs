using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Creates one immutable request-relative broker receipt selection shared by all closing source families.</summary>
public sealed class RecordedSourceReceiptCutFactory(IDomainEventReader events, INativeEventReceiptReader receipts)
{
    public RecordedSourceReceiptCut Create(Uuid tenant, DateTimeOffset through) => new(tenant, through, events, receipts);
}
