using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts.Readers;

/// <summary>Verifies the exact committed native record and returns its broker-assigned receipt time; never occurrence/preparation time.</summary>
public interface INativeEventReceiptReader
{
    ValueTask<DateTimeOffset> RecordedAtAsync(DomainEventRecord source, CancellationToken ct = default);
}
