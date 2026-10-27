using Cntryl.Portia;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Revises an application's metadata. <paramref name="CommandId" /> is the logical identity of the operation. The
///     three field intents are adapter-owned presence flags (they are not wire members) and are always carried into
///     <see cref="ApplicationUpdateAuthoredIntent" />, the one canonical shape every transport and queue uses.
/// </summary>
[Discriminator("bdgrz.application.revise", 1)]
public sealed record ReviseApplication(Uuid CommandId, Uuid TenantId, Uuid ApplicationId, long ExpectedRevision,
    string Name, string Purpose, string? OwnerReference, string? Classification = null, Uuid? SystemOwnerPersonId = null,
    Uuid? AccessOwnerPersonId = null, bool? IsRestricted = null,
    string? ApplicationKind = null, string? Criticality = null)
    : IRequest, IApplicationInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    [JsonIgnore]
    public ApplicationRevisionField ClassificationIntent { get; init; }
    [JsonIgnore]
    public ApplicationRevisionField SystemOwnerIntent { get; init; }
    [JsonIgnore]
    public ApplicationRevisionField AccessOwnerIntent { get; init; }

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
