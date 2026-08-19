using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace FixedIncome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly ICreateAssetUseCase _createAssetUseCase;
    private readonly IGetAllAssetsUseCase _getAllAssetsUseCase;
    private readonly IGetAssetByIdUseCase _getAssetByIdUseCase;
    private readonly IUpdateAssetUseCase _updateAssetUseCase;
    private readonly IDeleteAssetUseCase _deleteAssetUseCase;

    public AssetsController(
        ICreateAssetUseCase createAssetUseCase,
        IGetAllAssetsUseCase getAllAssetsUseCase,
        IGetAssetByIdUseCase getAssetByIdUseCase,
        IUpdateAssetUseCase updateAssetUseCase,
        IDeleteAssetUseCase deleteAssetUseCase)
    {
        _createAssetUseCase = createAssetUseCase;
        _getAllAssetsUseCase = getAllAssetsUseCase;
        _getAssetByIdUseCase = getAssetByIdUseCase;
        _updateAssetUseCase = updateAssetUseCase;
        _deleteAssetUseCase = deleteAssetUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequest request)
    {
        var response = await _createAssetUseCase.ExecuteAsync(request);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AssetResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _getAllAssetsUseCase.ExecuteAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _getAssetByIdUseCase.ExecuteAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssetRequest request)
    {
        var response = await _updateAssetUseCase.ExecuteAsync(id, request);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _deleteAssetUseCase.ExecuteAsync(id);
        return StatusCode(response.StatusCode, response);
    }
}
