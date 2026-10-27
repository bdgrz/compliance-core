using Bdgrz.Compliance.Features.Exports.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports.Commands;

/// <summary>Records one exact export build result caused by its persisted build trigger.</summary>
[Discriminator("bdgrz.exports.record-client-export-build-result", 2)]
public sealed record RecordClientExportBuildResult(
    Uuid TenantId,
    Uuid ExportId,
    long ExpectedRevision,
    Uuid BuildRequestEventId,
    ClientExportBuildResultContent Result,
    DateTimeOffset CompletedAt) : IRequest;
