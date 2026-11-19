using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>The owning context's terminal answer for one entry in one template-version application.</summary>
public sealed record TemplateEntryOutcome(string EntryKey, string Outcome, Uuid? RecordId, string? RefusalReason,
    NativeFactReference? DestinationSource = null);
