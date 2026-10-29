using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

public interface IEmailOwnershipRequest : IRequestBase
{
    Uuid UserId { get; }
}
