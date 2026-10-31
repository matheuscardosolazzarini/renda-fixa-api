using System.ComponentModel.DataAnnotations;
using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FixedIncome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IndexesController : ControllerBase
{
    private readonly IGetIndexRatesUseCase _getIndexRatesUseCase;

    public IndexesController(IGetIndexRatesUseCase getIndexRatesUseCase)
    {
        _getIndexRatesUseCase = getIndexRatesUseCase;
    }

    // [Required] em DateOnly? faz o [ApiController] responder 400 antes de o método ser chamado,
    // por isso o acesso a .Value é seguro. Em DateOnly sem ? a ausência não geraria erro.
    [HttpGet("{indexType}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<IndexRateResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<IndexRateResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRates(
        IndexType indexType,
        [FromQuery, Required] DateOnly? from,
        [FromQuery, Required] DateOnly? to)
    {
        var response = await _getIndexRatesUseCase.ExecuteAsync(indexType, from!.Value, to!.Value);
        return StatusCode(response.StatusCode, response);
    }
}
