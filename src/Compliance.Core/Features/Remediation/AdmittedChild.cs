using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>One admitted action in a closure's frozen manifest: its id and the admission fact on the finding.</summary>
public sealed record AdmittedChild(Uuid ChildId, NativeFactReference Admission);
