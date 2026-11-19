using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Compares the selected publication with the actual last applied publication, including skipped versions.</summary>
static class TemplateApplicationEntries
{
    public static (string[] Create, string[] Propose) Plan(IReadOnlyList<MethodologyTemplateRecordContent>? previous,
        IReadOnlyList<MethodologyTemplateRecordContent> selected)
    {
        var previousContent = previous?.ToDictionary(entry => entry.EntryKey,
            entry => ContentDigest.OfJson(entry), StringComparer.Ordinal) ?? [];
        var changed = selected.Where(entry => !previousContent.TryGetValue(entry.EntryKey, out var digest) ||
            digest != ContentDigest.OfJson(entry)).ToArray();
        return (changed.Where(entry => !previousContent.ContainsKey(entry.EntryKey)).Select(entry => entry.EntryKey)
                .Order(StringComparer.Ordinal).ToArray(),
            changed.Where(entry => previousContent.ContainsKey(entry.EntryKey)).Select(entry => entry.EntryKey)
                .Order(StringComparer.Ordinal).ToArray());
    }
}
