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

        // Lista única: posição, título, projeção e data usada. Alimenta os totais (RN-07) e os itens de
        // Positions, para a soma dos itens fechar exatamente com os totais, sem recalcular.
        var projected = new List<(Position Position, FixedIncomeAsset Asset, PositionProjection Projection, DateOnly Date)>();

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

            // R-02: com data explícita, a posição aportada depois dela não existia na data e fica
            // fora do consolidado (e do carregamento de mercado), em vez de derrubá-lo com 400.
            // Sem data informada vale a regra da data padrão, mais abaixo.
            if (referenceDate is not null)
            {
                positions = positions.Where(p => p.ApplicationDate <= referenceDate.Value).ToList();
            }
        }

        // Data do resumo: a informada, ou a padrão resolvida uma única vez, inclusive com a carteira
        // vazia (o campo nunca é nulo). Fica depois do 404 e do filtro por data, e dentro do
        // tratamento de DomainException, para uma falha do calendário virar 400.
        DateOnly summaryDate;

        try
        {
            summaryDate = referenceDate ?? await ResolveDefaultReferenceDateAsync();

            if (positions.Count > 0)
            {
                // Sem data informada, o aporte posterior à data padrão rende zero: a posição é
                // projetada na própria data do aporte, sem exigir cotação. Com data informada, o
                // aporte posterior já foi excluído pelo filtro acima (R-02) e nunca chega aqui.
                DateOnly DateFor(Position position) =>
                    referenceDate is null && position.ApplicationDate > summaryDate
                        ? position.ApplicationDate
                        : summaryDate;

                // Um único carregamento cobre todas as posições, do aporte mais antigo à maior data.
                var market = await _marketDataLoader.LoadAsync(
                    positions.Min(p => p.ApplicationDate), positions.Max(DateFor));

                foreach (var position in positions)
                {
                    var asset = assets[position.AssetId];

                    try
                    {
                        var date = DateFor(position);
                        var projection = position.Project(asset, date, market);
                        projected.Add((position, asset, projection, date));
                    }
                    // R-10: com várias posições, "Cotação de CDI ausente" sozinha não diz qual falhou,
                    // e a causa mais comum é a importação desligada ou ainda não executada. Outros
                    // erros de domínio seguem com a mensagem original (tratados no catch externo).
                    catch (DomainException ex) when (ex.Message.StartsWith("Cotação de CDI ausente", StringComparison.Ordinal))
                    {
                        return ApiResponse<PortfolioSummaryResponse>.BadRequest(
                            $"Posição {position.Id} (título '{asset.Name}'): {ex.Message} "
                            + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.");
                    }
                }
            }
        }
        catch (DomainException ex)
        {
            return ApiResponse<PortfolioSummaryResponse>.BadRequest(ex.Message);
        }

        var summary = PortfolioSummary.Create(projected.Select(p => (p.Projection, p.Asset.AssetType)));

        // Mesma ordem do repositório (D-6) e a mesma projeção que alimentou os totais.
        var items = projected
            .Select(p => new PortfolioSummaryResponse.PortfolioPositionResponse(
                PositionProjectionResponse.FromProjection(
                    p.Position.Id, p.Asset.Id, p.Projection, p.Date, p.Position.ApplicationDate),
                AssetResponse.FromEntity(p.Asset)))
            .ToList();

        var response = PortfolioSummaryResponse.FromSummary(summary, summaryDate, items);

        return ApiResponse<PortfolioSummaryResponse>.Ok(response);
    }

    // Achado I2: sem data, o resumo usa o dia útil seguinte à última cotação de CDI importada, nunca
    // a data de hoje, para o resumo padrão não falhar por falta da cotação de ontem. Sem nenhuma
    // cotação de CDI importada (banco recém-criado, importação ainda não rodou) não há data de
    // mercado a seguir: usa-se a data de hoje em Brasília (R-01, nunca a data UTC, que entre 21h e
    // 24h já é a do dia seguinte), via fuso do TimeProvider, e posições em CDI devolvem 400 com a
    // primeira data sem cotação (RN-09), enquanto as pré-fixadas e IPCA+ seguem calculando.
    private async Task<DateOnly> ResolveDefaultReferenceDateAsync()
    {
        return await _marketDataLoader.GetDefaultReferenceDateAsync()
            ?? DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
    }
}
