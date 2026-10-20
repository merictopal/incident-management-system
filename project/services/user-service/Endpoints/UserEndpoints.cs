using System.Security.Claims;
using ServiceDefaults.Auth;
using UserService.Contracts;
using UserService.Data;
using UserService.Security;
using static ServiceDefaults.Hosting.ServiceDefaultsExtensions;

namespace UserService.Endpoints;

public static class UserEndpoints
{
    public const int MaxNameLength = 60;
    public const int MinPasswordLength = 4;
    public const int MaxPasswordLength = 128;

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users");

        group.MapPost("/login", Login).AllowAnonymous();
        group.MapGet("/me", GetMe).RequireAuthorization();
        group.MapGet("/", GetAll).RequireAuthorization(Roles.ItSupportPolicy);
        group.MapGet("/{id:int}", GetById).RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Logs in with name + password + role. If no user with this name exists for
    /// the role yet, it is registered; otherwise the password must match.
    /// The same name and role therefore always map to the same user ID.
    /// </summary>
    private static IResult Login(LoginRequest request, UserStore store, TokenService tokens)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return ValidationError("name", "Name is required.");
        if (name.Length > MaxNameLength) return ValidationError("name", $"Name must be at most {MaxNameLength} characters.");

        var password = request.Password ?? string.Empty;
        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            return ValidationError("password", $"Password must be {MinPasswordLength}-{MaxPasswordLength} characters.");

        var role = Roles.Normalize(request.Role);
        if (role is null) return ValidationError("role", "Role must be Employee or IT Support.");

        var (user, created) = store.FindOrCreate(name, role, () => PasswordHasher.Hash(password));

        if (!created && !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return Results.Problem(
                title: "Invalid credentials",
                detail: "This name is already registered with a different password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var (token, expiresAt) = tokens.CreateToken(user);
        var response = new LoginResponse(token, expiresAt, UserDto.From(user), created);
        return created
            ? Results.Created($"/api/users/{user.Id}", response)
            : Results.Ok(response);
    }

    private static IResult GetMe(ClaimsPrincipal principal, UserStore store) =>
        int.TryParse(principal.GetUserId(), out var id) && store.GetById(id) is { } user
            ? Results.Ok(UserDto.From(user))
            : Results.NotFound();

    private static IResult GetAll(UserStore store) =>
        Results.Ok(store.GetAll().Select(UserDto.From));

    /// <summary>IT Support can look up anyone; employees can only look up themselves.</summary>
    private static IResult GetById(int id, ClaimsPrincipal principal, UserStore store)
    {
        if (!principal.IsItSupport() && principal.GetUserId() != id.ToString()) return Results.Forbid();
        return store.GetById(id) is { } user
            ? Results.Ok(UserDto.From(user))
            : Results.NotFound(new { message = $"User #{id} was not found." });
    }
}
