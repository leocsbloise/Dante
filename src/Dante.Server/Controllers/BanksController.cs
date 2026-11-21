using Dante.Banks;
using Dante.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        var catalog = await banks.ListAsync(cancellationToken);
        return Ok(ApiResponse.WithData(StatusCodes.Status200OK, "Banks loaded.", catalog));
    }
}
