using Microsoft.AspNetCore.Mvc;
using QBC.Api.Catalog;
using QBC.Api.Dtos;

namespace QBC.Api.Controllers;

[ApiController]
[Route("api/plans")]
public sealed class PlansController : ControllerBase
{
    /// <summary>Public list of membership tiers.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<PlanDto>> Get() =>
        Ok(PlanCatalog.Plans.Select(p => new PlanDto(
            p.Id, p.Name, p.PriceCents, p.Currency, p.Cycle, p.Tagline, p.Features, p.Featured)));
}
