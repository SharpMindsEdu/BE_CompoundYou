using Domain.Entities;

namespace Application.Features.Users.Services;

public interface ITokenService
{
    string CreateToken(User user);
}
