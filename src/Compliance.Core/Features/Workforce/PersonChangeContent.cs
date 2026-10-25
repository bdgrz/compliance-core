using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Exact producing native transition and prior values; historical bodies are never selected from live current state.</summary>
public sealed record PersonChangeContent(PersonHeader? Before, ImmutableContentRef? BeforeContent,
    PersonHeader After, ImmutableContentRef Content, string CommandKind, bool Changed);
