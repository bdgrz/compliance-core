using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Command subject and primary logical revision scope.</summary>
public sealed record OperationSubject(EventStreamAddress AffectedOwner, EventStreamAddress ExpectedRevisionOwner);
