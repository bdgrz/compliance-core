using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Kernel;

/// <summary>Deterministic identities for effects caused by a source event.</summary>
public static class EffectIdentity
{
    /// <summary>Command id of one effect of a source event; a null episode means the initial episode.</summary>
    public static Uuid CommandId(Uuid sourceEventId, string effectKind, string target, string? episode) =>
        Uuid.CreateVersion5(sourceEventId, Encoding.UTF8.GetBytes($"{effectKind}:{target}:{episode ?? "initial"}"));
}
