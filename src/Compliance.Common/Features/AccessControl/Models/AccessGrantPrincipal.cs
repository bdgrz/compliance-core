using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record AccessGrantPrincipal(AccessGrantPrincipalKind Kind, Uuid Id);
