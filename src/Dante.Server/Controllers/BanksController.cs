using Dante.Banks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dante.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/banks")]
public sealed class BanksController(IBankQueryService banks) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BankResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await banks.ListAsync(cancellationToken));
    }
}
