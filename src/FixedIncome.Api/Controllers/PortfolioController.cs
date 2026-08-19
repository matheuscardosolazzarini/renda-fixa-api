using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace FixedIncome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly IGetPortfolioSummaryUseCase _getPortfolioSummaryUseCase;

    public PortfolioController(IGetPortfolioSummaryUseCase getPortfolioSummaryUseCase)
    {
        _getPortfolioSummaryUseCase = getPortfolioSummaryUseCase;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PortfolioSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PortfolioSummaryResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PortfolioSummaryResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary([FromQuery] DateOnly? referenceDate)
    {
        var response = await _getPortfolioSummaryUseCase.ExecuteAsync(referenceDate);
        return StatusCode(response.StatusCode, response);
    }
}
