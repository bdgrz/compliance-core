using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     Records one personal link-existing or create-new decision for a staged row. <paramref name="CommandId" /> is the
///     logical operation identity; <paramref name="ExpectedRevision" /> is the batch's logical revision.
/// </summary>
[Discriminator("bdgrz.application_import.correlate_row", 2)]
public sealed record CorrelateApplicationImportRow(Uuid CommandId, Uuid TenantId, Uuid BatchId,
    Uuid RowId, long ExpectedRevision, string Decision, Uuid? ApplicationId,
    long? ExpectedApplicationRevision, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
