using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.UseCases;

public interface ICreatePositionUseCase
{
    Task<ApiResponse<PositionResponse>> ExecuteAsync(CreatePositionRequest request);
}

public class CreatePositionUseCase : ICreatePositionUseCase
{
    private readonly IPositionRepository _positionRepository;
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly IHolidayRepository _holidayRepository;

    public CreatePositionUseCase(
        IPositionRepository positionRepository,
        IFixedIncomeAssetRepository assetRepository,
        IHolidayRepository holidayRepository)
    {
        _positionRepository = positionRepository;
        _assetRepository = assetRepository;
        _holidayRepository = holidayRepository;
    }

    public async Task<ApiResponse<PositionResponse>> ExecuteAsync(CreatePositionRequest request)
    {
        var asset = await _assetRepository.GetByIdAsync(request.AssetId);

        if (asset is null)
        {
            return ApiResponse<PositionResponse>.NotFound($"Título com id {request.AssetId} não encontrado.");
        }

        try
        {
            var position = new Position(asset, request.InvestedAmount, request.ApplicationDate);

            // B-02 (RN-08): aporte que o calendário não cobre nunca poderia ser projetado e
            // derrubaria o consolidado depois; é rejeitado já no registro.
            var calendar = new BusinessDayCalendar(await _holidayRepository.GetAllAsync());
            calendar.EnsureCovers(request.ApplicationDate, "Data de aporte");

            await _positionRepository.AddAsync(position);

            return ApiResponse<PositionResponse>.Created(PositionResponse.FromEntity(position));
        }
        catch (DomainException ex)
        {
            return ApiResponse<PositionResponse>.BadRequest(ex.Message);
        }
    }
}
