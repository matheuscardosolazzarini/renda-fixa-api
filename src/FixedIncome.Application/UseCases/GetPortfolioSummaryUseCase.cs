using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.UseCases;

public interface IGetPortfolioSummaryUseCase
{
    Task<ApiResponse<PortfolioSummaryResponse>> ExecuteAsync(DateOnly? referenceDate);
}

public class GetPortfolioSummaryUseCase : IGetPortfolioSummaryUseCase
{
    private readonly IPositionRepository _positionRepository;
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly MarketDataLoader _marketDataLoader;
    private readonly TimeProvider _timeProvider;

    public GetPortfolioSummaryUseCase(
        IPositionRepository positionRepository,
        IFixedIncomeAssetRepository assetRepository,
        MarketDataLoader marketDataLoader,
        TimeProvider timeProvider)
    {
        _positionRepository = positionRepository;
        _assetRepository = assetRepository;
        _marketDataLoader = marketDataLoader;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<PortfolioSummaryResponse>> ExecuteAsync(DateOnly? referenceDate)
    {
        var positions = (await _positionRepository.GetAllAsync()).ToList();

        // Títulos carregados uma única vez e resolvidos em memória por AssetId: várias
        // posições costumam apontar para o mesmo título, e consultar o repositório dentro
        // do laço abaixo multiplicaria o número de idas ao banco por posição.
        var assets = (await _assetRepository.GetAllAsync()).ToDictionary(asset => asset.Id);

        var projections = new List<(PositionProjection Projection, AssetType AssetType)>();

        if (positions.Count > 0)
        {
            // Existência dos títulos antes de carregar o mercado: o 404 não pode ser encoberto
            // por um erro de calendário do carregamento.
            foreach (var position in positions)
            {
                if (!assets.ContainsKey(position.AssetId))
                {
                    return ApiResponse<PortfolioSummaryResponse>.NotFound(
                        $"Título vinculado à posição {position.Id} não encontrado.");
                }
            }

            try
            {
                var defaultDate = referenceDate ?? await ResolveDefaultReferenceDateAsync();

                // Sem data informada, o aporte posterior à data padrão rende zero: a posição é
                // projetada na própria data do aporte, sem exigir cotação. Com data explícita
                // nada muda e o aporte posterior segue sendo erro de domínio.
                DateOnly DateFor(Position position) =>
                    referenceDate is null && position.ApplicationDate > defaultDate
                        ? position.ApplicationDate
                        : defaultDate;

                // Um único carregamento cobre todas as posições, do aporte mais antigo à maior data.
                var market = await _marketDataLoader.LoadAsync(
                    positions.Min(p => p.ApplicationDate), positions.Max(DateFor));

                foreach (var position in positions)
                {
                    var asset = assets[position.AssetId];
                    var projection = position.Project(asset, DateFor(position), market);
                    projections.Add((projection, asset.AssetType));
                }
            }
            catch (DomainException ex)
            {
                return ApiResponse<PortfolioSummaryResponse>.BadRequest(ex.Message);
            }
        }

        var summary = PortfolioSummary.Create(projections);
        var response = PortfolioSummaryResponse.FromSummary(summary);

        return ApiResponse<PortfolioSummaryResponse>.Ok(response);
    }

    // Achado I2: sem data, o resumo usa o dia útil seguinte à última cotação de CDI importada, nunca
    // a data de hoje, para o resumo padrão não falhar por falta da cotação de ontem. Sem nenhuma
    // cotação de CDI importada (banco recém-criado, importação ainda não rodou) não há data de
    // mercado a seguir: usa-se a data de hoje do TimeProvider, e posições em CDI devolvem 400 com a
    // primeira data sem cotação (RN-09), enquanto as pré-fixadas e IPCA+ seguem calculando.
    private async Task<DateOnly> ResolveDefaultReferenceDateAsync()
    {
        return await _marketDataLoader.GetDefaultReferenceDateAsync()
            ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    }
}
