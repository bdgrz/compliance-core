using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Binds batch preparation to its complete captured native payload/identity and physical cut.</summary>
sealed record ApplicationImportBatchPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static ApplicationImportBatchPreparedCut Capture(ApplicationImportBatch batch) =>
        new(batch.Stream, batch.Version, batch.CommittedStreamPosition, batch.NativeCut.Digest);
    public bool Matches(ApplicationImportBatch batch) => this == Capture(batch);
    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The import batch changed while its exact source-derived content was prepared.", true));
}
