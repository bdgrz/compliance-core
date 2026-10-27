using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Exports.Commands;

/// <summary>Records the request client export intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.exports.request-client-export", 1)]
public sealed record RequestClientExport(Uuid TenantId, Uuid ExportId, string Scope, string Format, string ExportKey) : IRequest, IClientManagementMutationRequest, ICallable;
