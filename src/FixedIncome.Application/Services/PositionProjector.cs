using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.Services;

// Projeta um conjunto de aportes com as regras de data do resumo (D-11), para o consolidado e a
// projeção por título não divergirem. Classe concreta, sem interface: não cruza camada.
public class PositionProjector
{
    private readonly MarketDataLoader _marketDataLoader;
    private readonly TimeProvider _timeProvider;

    public PositionProjector(MarketDataLoader marketDataLoader, TimeProvider timeProvider)
    {
        _marketDataLoader = marketDataLoader;
        _timeProvider = timeProvider;
    }

    // O chamador já verificou (404) que todo AssetId dos aportes está em assets: a existência dos
    // títulos vem antes do carregamento de mercado, para o 404 não ser encoberto por erro de
    // calendário. Falhas de domínio (calendário, cotação ausente) saem como DomainException.
    public async Task<ProjectionResult> ProjectAsync(
        IEnumerable<Position> positions, IReadOnlyDictionary<Guid, FixedIncomeAsset> assets, DateOnly? referenceDate)
    {
        var included = positions.ToList();

        // R-02: com data explícita, a posição aportada depois dela não existia na data e fica
        // fora do consolidado (e do carregamento de mercado), em vez de derrubá-lo com 400.
        // Sem data informada vale a regra da data padrão, mais abaixo.
        if (referenceDate is not null)
        {
            included = included.Where(p => p.ApplicationDate <= referenceDate.Value).ToList();
        }

        // Data do resultado: a informada, ou a padrão resolvida uma única vez, inclusive sem aportes
        // (o campo nunca é nulo).
        var resolvedDate = referenceDate ?? await ResolveDefaultReferenceDateAsync();

        var items = new List<ProjectedPosition>();

        if (included.Count > 0)
        {
            // Sem data informada, o aporte posterior à data padrão rende zero: a posição é
            // projetada na própria data do aporte, sem exigir cotação. Com data informada, o
            // aporte posterior já foi excluído pelo filtro acima (R-02) e nunca chega aqui.
            DateOnly DateFor(Position position) =>
                referenceDate is null && position.ApplicationDate > resolvedDate
                    ? position.ApplicationDate
                    : resolvedDate;

            // Um único carregamento cobre todas as posições, do aporte mais antigo à maior data.
            var market = await _marketDataLoader.LoadAsync(
                included.Min(p => p.ApplicationDate), included.Max(DateFor));

            foreach (var position in included)
            {
                var asset = assets[position.AssetId];

                try
                {
                    var date = DateFor(position);
                    var projection = position.Project(asset, date, market);
                    items.Add(new ProjectedPosition(position, asset, projection, date));
                }
                // R-10: com várias posições, "Cotação de CDI ausente" sozinha não diz qual falhou,
                // e a causa mais comum é a importação desligada ou ainda não executada. Outros
                // erros de domínio seguem com a mensagem original.
                catch (DomainException ex) when (ex.Message.StartsWith("Cotação de CDI ausente", StringComparison.Ordinal))
                {
                    throw new DomainException(
                        $"Posição {position.Id} (título '{asset.Name}'): {ex.Message} "
                        + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) "
                        + "ou ainda não ter rodado.");
                }
            }
        }

        return new ProjectionResult(resolvedDate, items);
    }

    // Achado I2: sem data, usa o dia útil seguinte à última cotação de CDI importada, nunca
    // a data de hoje, para o resumo padrão não falhar por falta da cotação de ontem. Sem nenhuma
    // cotação de CDI importada (banco recém-criado, importação ainda não rodou) não há data de
    // mercado a seguir: usa-se a data de hoje em Brasília (R-01, nunca a data UTC, que entre 21h e
    // 24h já é a do dia seguinte), via fuso do TimeProvider, e posições em CDI falham com a
    // primeira data sem cotação (RN-09), enquanto as pré-fixadas e IPCA+ seguem calculando.
    private async Task<DateOnly> ResolveDefaultReferenceDateAsync()
    {
        return await _marketDataLoader.GetDefaultReferenceDateAsync()
            ?? DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
    }
}

// Projeção de um aporte com o título e a data efetivamente usada (D-6: mesma ordem de entrada).
public sealed record ProjectedPosition(
    Position Position, FixedIncomeAsset Asset, PositionProjection Projection, DateOnly Date);

public sealed record ProjectionResult(DateOnly ReferenceDate, IReadOnlyList<ProjectedPosition> Items);
