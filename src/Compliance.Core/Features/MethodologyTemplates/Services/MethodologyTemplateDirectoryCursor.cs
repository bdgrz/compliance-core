using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Opaque native keyset cursor scoped to the full Portia projection identity.</summary>
static class MethodologyTemplateDirectoryCursor
{
    public static string Encode(string projectionKey, long ticks, Uuid templateId) => projectionKey;
    public static bool TryDecode(string projectionKey, string? cursor, out long ticks, out Uuid templateId)
    {
        ticks = 0;
        templateId = Uuid.Empty;
        return projectionKey == cursor && cursor is null;
    }
}
