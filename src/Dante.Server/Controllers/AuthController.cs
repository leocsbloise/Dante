using Dante.Infrastructure;
using Dante.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dante.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await auth.RegisterAsync(request, cancellationToken);
        return Created(
            $"/api/users/{user.Id}",
            ApiResponse.WithData(StatusCodes.Status201Created, "Account created.", user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var token = await auth.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse.WithData(StatusCodes.Status200OK, "Signed in.", token));
    }
}
