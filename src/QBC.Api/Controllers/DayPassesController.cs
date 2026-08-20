using Microsoft.AspNetCore.Mvc;
using QBC.Api.Catalog;
using QBC.Api.Dtos;

namespace QBC.Api.Controllers;

[ApiController]
[Route("api/day-passes")]
public sealed class DayPassesController : ControllerBase
{
    /// <summary>Public list of one-time day-pass products (with the booking window).</summary>
    [HttpGet("products")]
    public ActionResult<IEnumerable<DayPassProductDto>> Products() =>
        Ok(DayPassCatalog.Products.Select(p => new DayPassProductDto(
            p.Id, p.Name, p.PriceCents, p.Currency, p.Description, DayPassCatalog.MaxDaysAhead)));
}
