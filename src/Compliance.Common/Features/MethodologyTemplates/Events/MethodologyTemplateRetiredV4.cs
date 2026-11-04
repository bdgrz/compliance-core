using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>An idle methodology version line was retired with a reason.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-retired", 4)]
public sealed record MethodologyTemplateRetiredV4(Uuid TemplateId, string Reason, Uuid RetiredBy) : DomainEvent;
