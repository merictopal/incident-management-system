using UserService.Models;

namespace UserService.Contracts;

public sealed record LoginRequest(string? Name, string? Password, string? Role);

/// <summary>Public view of a user (no password hash).</summary>
public sealed record UserDto(int Id, string Name, string Role, DateTimeOffset CreatedAt)
{
    public static UserDto From(User user) => new(user.Id, user.Name, user.Role, user.CreatedAt);
}

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User, bool IsNewUser);
