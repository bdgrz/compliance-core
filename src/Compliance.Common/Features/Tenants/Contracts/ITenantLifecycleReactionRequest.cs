using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Contracts;

/// <summary>Marks tenant lifecycle commands reserved for trusted Portia system execution.</summary>
public interface ITenantLifecycleReactionRequest : IRequestBase;
