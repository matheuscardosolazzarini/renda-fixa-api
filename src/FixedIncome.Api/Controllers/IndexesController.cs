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
        // O binder de enum aceita o número do valor (Enum.TryParse aceita dígitos), mas a API só
        // documenta o nome: o número também é rejeitado no corpo das demais rotas.
        var rawValue = Request.RouteValues["indexType"] as string;
        if (!string.Equals(Enum.GetName(indexType), rawValue?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var invalid = ApiResponse<IEnumerable<IndexRateResponse>>.BadRequest(
                "O valor do campo 'indexType' é inválido.");
            return StatusCode(invalid.StatusCode, invalid);
        }

        var response = await _getIndexRatesUseCase.ExecuteAsync(indexType, from!.Value, to!.Value);
        return StatusCode(response.StatusCode, response);
    }
}
