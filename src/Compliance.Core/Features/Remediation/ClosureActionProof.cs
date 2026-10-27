using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The held completion of one action: the completion fact and the grant that holds it.</summary>
public sealed record ClosureActionProof(Uuid ActionId, NativeFactReference Completion, NativeFactReference Grant);
