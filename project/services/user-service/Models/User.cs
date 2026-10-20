namespace UserService.Models;

/// <summary>A registered user. The password hash is never returned by the API.</summary>
public sealed record User(int Id, string Name, string Role, DateTimeOffset CreatedAt, string PasswordHash);
