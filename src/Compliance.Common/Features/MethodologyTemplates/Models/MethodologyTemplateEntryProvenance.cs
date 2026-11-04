using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>The exact firm template version and entry that supplied a destination draft or proposal.</summary>
public sealed record MethodologyTemplateEntryProvenance(Uuid TemplateId, long Version, string EntryKey);
