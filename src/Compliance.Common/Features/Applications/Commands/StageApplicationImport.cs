using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Stages one immutable observation of a source. <paramref name="SubmissionId" /> is the logical operation
///     identity: an exact retry replays the original registration, a changed observation conflicts. The complete row
///     content is stored as immutable content, so the observation is not capped by an event size.
/// </summary>
[Discriminator("bdgrz.application_import.stage", 1)]
public sealed record StageApplicationImport(Uuid TenantId, Uuid SubmissionId,
    string SourceKey, string SourceNamespace, string Coverage,
    IReadOnlyList<ApplicationImportInputRow> Rows)
    : IRequest<ApplicationImportRegistration>, IApplicationInventoryWriteRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => SubmissionId;
}
