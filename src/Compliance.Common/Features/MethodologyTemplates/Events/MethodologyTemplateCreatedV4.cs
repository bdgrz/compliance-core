using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>A named methodology version line was created idle, without an authored draft.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-created", 4)]
public sealed record MethodologyTemplateCreatedV4(Uuid TemplateId, string Name, string Purpose, Uuid CreatedBy) : DomainEvent;
