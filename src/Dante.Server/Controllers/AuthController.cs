using Dante.Users;
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
        return Created($"/api/users/{user.Id}", user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return Ok(await auth.LoginAsync(request, cancellationToken));
    }
}
