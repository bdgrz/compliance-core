using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Kernel;

/// <summary>Test-only authored intent with required, optional, timestamp and decimal fields.</summary>
sealed record DigestIntent(Uuid Id, string Name, string? Note, ulong? Revision, DateTimeOffset At,
    decimal Amount);
