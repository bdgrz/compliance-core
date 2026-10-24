using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Command subject and primary logical revision scope.</summary>
public sealed record OperationSubject(EventStreamAddress AffectedOwner, EventStreamAddress ExpectedRevisionOwner);
