using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>One canonical ordering decision pins a published version and the exact destination entry plan.
/// AppliedBy is the original personal UserId, retained for currently authorized destination dispatch.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-applied", 4)]
public sealed record MethodologyTemplateAppliedV4(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, long PreviousVersion, Uuid CommandId, Uuid AppliedBy,
    NativeFactReference PublicationSource, ulong PublicationPosition, ImmutableContentRef EntriesContent,
    IReadOnlyList<string> EntriesToCreate, IReadOnlyList<string> EntriesToPropose) : DomainEvent;
