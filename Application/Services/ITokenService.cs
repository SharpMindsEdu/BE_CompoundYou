using Domain.Entities;

namespace Application.Services;

public interface ITokenService
{
    string CreateToken(User user);
}