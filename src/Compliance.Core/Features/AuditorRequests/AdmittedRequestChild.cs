using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>One admitted child in a resolution's frozen manifest: its id, its kind and the admission fact on the request.</summary>
public sealed record AdmittedRequestChild(Uuid ChildId, AdmittedRequestChildKind Kind, NativeFactReference Admission);
