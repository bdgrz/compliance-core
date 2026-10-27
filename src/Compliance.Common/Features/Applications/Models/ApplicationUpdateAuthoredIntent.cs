using Bdgrz.Compliance.Features.Applications.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
///     The canonical authored intent of an application revision, shared by the HTTP binder, the MCP adapter and the
///     queue. Each field whose presence changes meaning carries an explicit <c>IsSupplied</c> flag, so keeping a field
///     (omitted) and clearing it (supplied null) never share an intent even though the command's presence helpers are
///     not wire members. An unsupplied field records a null value. Text is trimmed exactly as the owner trims it.
///     The command id is the logical identity of the operation, not part of the intent.
/// </summary>
public sealed record ApplicationUpdateAuthoredIntent(Uuid TenantId, Uuid ApplicationId, long ExpectedRevision,
    string Name, string Purpose, string? OwnerReference, string? Classification, bool ClassificationIsSupplied,
    Uuid? SystemOwnerPersonId, bool SystemOwnerIsSupplied, Uuid? AccessOwnerPersonId, bool AccessOwnerIsSupplied,
    bool? IsRestricted, string? ApplicationKind, string? Criticality)
{
    /// <summary>Canonicalizes a bound revision command.</summary>
    public static ApplicationUpdateAuthoredIntent From(ReviseApplication request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ApplicationUpdateAuthoredIntent(request.TenantId, request.ApplicationId, request.ExpectedRevision,
            request.Name?.Trim()!, request.Purpose?.Trim()!, Optional(request.OwnerReference),
            request.ClassificationIntent.IsSupplied ? request.Classification?.Trim() : null,
            request.ClassificationIntent.IsSupplied,
            request.SystemOwnerIntent.IsSupplied ? request.SystemOwnerPersonId : null,
            request.SystemOwnerIntent.IsSupplied,
            request.AccessOwnerIntent.IsSupplied ? request.AccessOwnerPersonId : null,
            request.AccessOwnerIntent.IsSupplied,
            request.IsRestricted, request.ApplicationKind?.Trim(), request.Criticality?.Trim());
    }

    /// <summary>Rebuilds the command, restoring the presence flags no public serializer retains.</summary>
    public ReviseApplication ToRequest(Uuid commandId) => new(commandId, TenantId, ApplicationId, ExpectedRevision,
        Name, Purpose, OwnerReference, Classification, SystemOwnerPersonId, AccessOwnerPersonId, IsRestricted,
        ApplicationKind, Criticality)
    {
        ClassificationIntent = new ApplicationRevisionField(ClassificationIsSupplied),
        SystemOwnerIntent = new ApplicationRevisionField(SystemOwnerIsSupplied),
        AccessOwnerIntent = new ApplicationRevisionField(AccessOwnerIsSupplied),
    };

    static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
