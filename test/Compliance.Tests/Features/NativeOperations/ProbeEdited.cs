using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

/// <summary>Test-only later edit that is not a native operation fact.</summary>
[Discriminator("test.probe.edited", 1)]
sealed record ProbeEdited(Uuid ProbeId, string Payload) : DomainEvent;
