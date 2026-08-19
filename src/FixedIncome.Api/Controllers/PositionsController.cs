using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace FixedIncome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PositionsController : ControllerBase
{
    private readonly ICreatePositionUseCase _createPositionUseCase;
    private readonly IGetAllPositionsUseCase _getAllPositionsUseCase;
    private readonly IGetPositionProjectionUseCase _getPositionProjectionUseCase;

    public PositionsController(
        ICreatePositionUseCase createPositionUseCase,
        IGetAllPositionsUseCase getAllPositionsUseCase,
        IGetPositionProjectionUseCase getPositionProjectionUseCase)
    {
        _createPositionUseCase = createPositionUseCase;
        _getAllPositionsUseCase = getAllPositionsUseCase;
        _getPositionProjectionUseCase = getPositionProjectionUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PositionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<PositionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PositionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreatePositionRequest request)
    {
        var response = await _createPositionUseCase.ExecuteAsync(request);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PositionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _getAllPositionsUseCase.ExecuteAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id}/projection")]
    [ProducesResponseType(typeof(ApiResponse<PositionProjectionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PositionProjectionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PositionProjectionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjection(Guid id, [FromQuery] DateOnly? referenceDate)
    {
        var date = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await _getPositionProjectionUseCase.ExecuteAsync(id, date);
        return StatusCode(response.StatusCode, response);
    }
}
