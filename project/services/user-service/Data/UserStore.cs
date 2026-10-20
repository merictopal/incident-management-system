using UserService.Models;

namespace UserService.Data;

/// <summary>
/// In-memory user registry. A single lock keeps "find or create" atomic, so two
/// simultaneous logins with the same name cannot create duplicate users.
/// </summary>
public sealed class UserStore
{
    private readonly Dictionary<int, User> _users = new();
    private readonly object _gate = new();
    private int _nextId = 1000;

    public IReadOnlyList<User> GetAll()
    {
        lock (_gate) return _users.Values.OrderBy(user => user.Id).ToList();
    }

    public User? GetById(int id)
    {
        lock (_gate) return _users.GetValueOrDefault(id);
    }

    /// <summary>Returns the existing user for (name, role) or creates one with the given password hash.</summary>
    public (User User, bool Created) FindOrCreate(string name, string role, Func<string> createPasswordHash)
    {
        lock (_gate)
        {
            var existing = _users.Values.FirstOrDefault(user =>
                string.Equals(user.Name, name, StringComparison.OrdinalIgnoreCase) &&
                user.Role == role);

            if (existing is not null) return (existing, false);

            var user = new User(++_nextId, name, role, DateTimeOffset.UtcNow, createPasswordHash());
            _users[user.Id] = user;
            return (user, true);
        }
    }
}
