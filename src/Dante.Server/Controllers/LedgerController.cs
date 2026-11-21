using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dante.Infrastructure;
using Dante.Ledger;
using Dante.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dante.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/ledger")]
public sealed class LedgerController(ILedgerService ledger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateLedgerRequest request, CancellationToken cancellationToken)
    {
        var entry = await ledger.CreateAsync(GetUserId(), request, cancellationToken);
        return Created(
            $"/api/ledger/{entry.Id}",
            ApiResponse.WithData(StatusCodes.Status201Created, "Movement recorded.", entry));
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] LedgerSearchRequest request, CancellationToken cancellationToken)
    {
        var page = await ledger.SearchAsync(GetUserId(), request, cancellationToken);
        return Ok(ApiResponse.WithData(StatusCodes.Status200OK, "Movements loaded.", page));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
            throw new InvalidCredentialsException();

        return userId;
    }
}
