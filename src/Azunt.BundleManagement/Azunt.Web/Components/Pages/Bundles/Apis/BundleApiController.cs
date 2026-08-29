using Azunt.BundleManagement;
using Microsoft.AspNetCore.Mvc;

namespace Azunt.Web.Components.Pages.Bundles.Apis;

[ApiController]
[Route("api/bundles")]
public class BundleApiController : ControllerBase
{
    private readonly IBundleRepository _repository;

    public BundleApiController(IBundleRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<Bundle>>> GetPaged(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20,
        [FromQuery] string searchQuery = "",
        [FromQuery] string sortOrder = "",
        [FromQuery] string? status = null,
        [FromQuery] bool activeOnly = false)
    {
        return Ok(await _repository.GetPagedAsync(new BundleFilterOptions
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            SearchQuery = searchQuery,
            SortOrder = sortOrder,
            Status = status,
            ActiveOnly = activeOnly
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Bundle>> GetById(int id)
    {
        var model = await _repository.GetByIdAsync(id);
        return model is null ? NotFound() : Ok(model);
    }

    [HttpPost]
    public async Task<ActionResult<Bundle>> Create(Bundle model)
    {
        var created = await _repository.AddAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Bundle model)
    {
        if (id != model.Id) return BadRequest("Route ID and model ID do not match.");
        return await _repository.UpdateAsync(model) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await _repository.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
