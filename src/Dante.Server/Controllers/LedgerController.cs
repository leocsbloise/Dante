using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dante.Ledger;
using Dante.Users;
using Microsoft.AspNetCore.Authorization;
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
        return Created($"/api/ledger/{entry.Id}", entry);
    }

    [HttpGet]
    public async Task<LedgerPage> Search([FromQuery] LedgerSearchRequest request, CancellationToken cancellationToken)
    {
        return await ledger.SearchAsync(GetUserId(), request, cancellationToken);
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
