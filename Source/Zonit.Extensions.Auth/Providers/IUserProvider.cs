namespace Zonit.Extensions.Auth;

public interface IUserProvider
{
    public Task<UserModel?> GetByIdAsync(Guid id);
    public Task<UserModel?> GetByUserNameAsync(string userName);
}