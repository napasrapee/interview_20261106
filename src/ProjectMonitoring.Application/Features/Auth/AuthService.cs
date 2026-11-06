using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Users;

namespace ProjectMonitoring.Application.Features.Auth;

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    ICurrentUser currentUser,
    UserService userService,
    IValidator<LoginRequest> validator)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var user = await users.FindByEmailAsync(request.Email, cancellationToken);

        // Verify first, so unknown and known accounts take the same time.
        var passwordMatches = passwordHasher.Verify(request.Password, user?.PasswordHash);
        if (user is null || !passwordMatches || !user.CanLogIn)
        {
            throw new InvalidCredentialsException();
        }

        var token = tokenIssuer.Issue(user);
        return new LoginResponse(token.Value, "Bearer", token.ExpiresInSeconds);
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id)
        {
            throw new UnauthorizedException("Authentication is required.");
        }

        try
        {
            return await userService.GetAsync(id, cancellationToken);
        }
        catch (NotFoundException)
        {
            // The token is valid, but its account has been deleted.
            throw new UnauthorizedException("Authentication is required.");
        }
    }
}
