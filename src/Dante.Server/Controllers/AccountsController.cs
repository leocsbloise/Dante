using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dante.Accounts;
using Dante.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dante.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(IAccountService accounts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await accounts.ListAsync(GetUserId(), cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await accounts.CreateAsync(GetUserId(), request, cancellationToken);
        return Created($"/api/accounts/{account.Id}", account);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await accounts.DeleteAsync(GetUserId(), id, cancellationToken);
        return NoContent();
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
