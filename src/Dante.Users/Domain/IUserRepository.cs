namespace Dante.Users;

public interface IUserRepository
{
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    void Add(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
