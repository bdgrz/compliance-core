using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Realizes one entry of an exact committed application; no caller-authored outcome is accepted.</summary>
[Discriminator("bdgrz.methodology-templates.apply-entry", 1)]
public sealed record ApplyMethodologyTemplateEntry(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, string EntryKey, NativeFactReference Source, ulong SourcePosition, Uuid CommandId)
    : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
