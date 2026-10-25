namespace Bdgrz.Compliance.Features.Work;

/// <summary>One finalized current/selected native and physical append cut, never a lifetime replay collection.</summary>
sealed record WeeklyDigestPreparation(ulong NativeRevision, ulong PhysicalPosition,
    WeeklyDigestMutationContent Change);
