using System.Globalization;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Entities;

public class Position : BaseEntity
{
    // Limites da coluna invested_amount, numeric(18,2): 16 dígitos inteiros e 2 casas decimais.
    public const decimal InvestedAmountLimit = 10_000_000_000_000_000m;
    public const int InvestedAmountDecimalPlaces = 2;

    public Guid AssetId { get; private set; }
    public decimal InvestedAmount { get; private set; }
    public DateOnly ApplicationDate { get; private set; }

    public Position(FixedIncomeAsset asset, decimal investedAmount, DateOnly applicationDate)
    {
        if (asset is null)
        {
            throw new DomainException("Asset é obrigatório.");
        }

        if (investedAmount <= 0)
        {
            throw new DomainException("InvestedAmount deve ser maior que zero.");
        }

        if (investedAmount >= InvestedAmountLimit)
        {
            // Stryker disable once String : InvestedAmountLimit tem escala 0 (10_000_000_000_000_000m), então ToString("0") e ToString("") (formato geral) produzem o mesmo texto
            throw new DomainException(
                $"InvestedAmount deve ser menor que {InvestedAmountLimit.ToString("0", CultureInfo.InvariantCulture)}.");
        }

        if (decimal.Round(investedAmount, InvestedAmountDecimalPlaces) != investedAmount)
        {
            throw new DomainException(
                $"InvestedAmount deve ter no máximo {InvestedAmountDecimalPlaces} casas decimais.");
        }

        if (applicationDate < asset.IssueDate || applicationDate > asset.MaturityDate)
        {
            throw new DomainException("ApplicationDate deve estar entre a emissão e o vencimento do título.");
        }

        AssetId = asset.Id;
        InvestedAmount = investedAmount;
        ApplicationDate = applicationDate;
    }

    // Construtor privado sem parâmetros, para materialização pelo EF Core.
    private Position()
    {
    }

    public PositionProjection Project(FixedIncomeAsset asset, DateOnly referenceDate, MarketData market)
    {
        ArgumentNullException.ThrowIfNull(market);

        if (asset.Id != AssetId)
        {
            throw new DomainException("O título informado não corresponde a esta posição.");
        }

        if (referenceDate < ApplicationDate)
        {
            throw new DomainException("A data de referência não pode ser anterior à data de aporte.");
        }

        // RN-02: a data de referência é limitada ao vencimento do título.
        // Stryker disable once Equality : com referenceDate == MaturityDate os dois ramos resultam na mesma data
        var calculationDate = referenceDate > asset.MaturityDate ? asset.MaturityDate : referenceDate;

        // Dias corridos são a base de IR e IOF; dias úteis (RN-08) são a base da rentabilidade.
        var elapsedDays = calculationDate.DayNumber - ApplicationDate.DayNumber;
        var businessDays = market.Calendar.BusinessDaysBetween(ApplicationDate, calculationDate);

        var isEstimated = false;
        var isProjected = false;
        var growthFactor = asset.IndexType switch
        {
            IndexType.PreFixed => PreFixedFactor(asset.Rate, businessDays.Count),
            IndexType.Cdi => CdiFactor(asset.Rate, businessDays, market, out isProjected),
            IndexType.Ipca => IpcaFactor(asset.Rate, businessDays, market, out isEstimated, out isProjected),
            _ => throw new DomainException($"IndexType não suportado: {asset.IndexType}.")
        };

        // Ordem de arredondamento (achado B1): cada valor sai do anterior já arredondado, para
        // que bruto - IOF - IR feche exatamente com o líquido e a RN-07 feche no consolidado.
        var investedAmount = Round2(InvestedAmount);
        var grossAmount = Round2(InvestedAmount * growthFactor);
        var grossYield = grossAmount - investedAmount;

        // RN-11: LCA tem IOF zero (Decreto 6.306/2007, art. 32, § 2º, V); LCI e Tesouro pagam.
        var iofRate = asset.AssetType == AssetType.Lca ? 0m : IofTable.RateFor(elapsedDays);
        var taxRate = IncomeTaxTable.RateFor(asset.AssetType, elapsedDays);

        // Sem rendimento positivo não há imposto, mas as alíquotas seguem as da tabela pelo prazo.
        var iofAmount = 0m;
        var taxAmount = 0m;

        // Stryker disable once Equality : com grossYield == 0 o ramo executado dá iofAmount = 0.00m e taxAmount = 0.00m, numericamente iguais aos 0m iniciais; só a escala difere
        if (grossYield > 0)
        {
            iofAmount = Round2(grossYield * iofRate / 100);

            // RN-03: o IR incide sobre o rendimento menos o IOF, nunca sobre o principal.
            taxAmount = Round2((grossYield - iofAmount) * taxRate / 100);
        }

        return new PositionProjection(
            InvestedAmount: investedAmount,
            GrossAmount: grossAmount,
            GrossYield: grossYield,
            TaxRate: Math.Round(taxRate, 1, MidpointRounding.AwayFromZero),
            TaxAmount: taxAmount,
            NetAmount: grossAmount - iofAmount - taxAmount,
            ElapsedDays: elapsedDays,
            BusinessDays: businessDays.Count,
            IofRate: iofRate,
            IofAmount: iofAmount,
            IsEstimated: isEstimated,
            IsProjected: isProjected);
    }

    // RN-01: capitalização composta em base 252 dias úteis.
    private static decimal PreFixedFactor(decimal annualRate, int businessDays)
    {
        return DecimalMath.Pow(1 + annualRate / 100, (decimal)businessDays / 252);
    }

    // RN-09 e RN-12: resolve a taxa de cada dia útil e delega o produtório a IndexAccumulation.
    // Dia com cotação usa a publicada. Sem cotação: anterior à última cotação do banco é lacuna
    // (falha de importação, sem fallback); sem nenhuma cotação, falha; posterior a ela é falha se
    // já devia estar publicado e projeção com o valor da última cotação se não.
    private static decimal CdiFactor(
        decimal percentOfCdi, IReadOnlyList<DateOnly> businessDays, MarketData market, out bool isProjected)
    {
        isProjected = false;
        var rates = new List<decimal>(businessDays.Count);

        foreach (var day in businessDays)
        {
            var published = market.GetCdi(day);

            if (published is not null)
            {
                rates.Add(published.Value);
                continue;
            }

            var last = market.LastCdi;

            if (last is null || day < last.Value.Date || CdiIsOverdue(day, market))
            {
                throw new DomainException($"Cotação de CDI ausente para {day:yyyy-MM-dd}.");
            }

            rates.Add(last.Value.Value);
            isProjected = true;
        }

        return IndexAccumulation.CdiFactor(rates, percentOfCdi);
    }

    // D-2: o CDI de um dia só sai no SGS no dia útil seguinte, então só é falha de importação o dia
    // sem cotação anterior ao dia útil anterior a hoje, ou seja, cujo dia útil seguinte já passou.
    // Dia igual ou posterior a hoje nunca é, e isso evita consultar o calendário fora do intervalo.
    private static bool CdiIsOverdue(DateOnly day, MarketData market)
    {
        return day < market.Today && market.Calendar.NextBusinessDay(day) < market.Today;
    }

    // RN-10 e RN-12: IPCA pro rata em dias úteis por mês civil (simplificação do achado B3: a
    // convenção de mercado usa a data de aniversário), vezes a taxa real em base 252. Mês sem
    // cotação anterior ao último publicado é lacuna. Posterior a ele, é estimado com a última
    // variação se já encerrou (isEstimated) e projetado com a média geométrica se é o mês de hoje ou
    // um seguinte (isProjected), porque o IPCA do mês só sai no mês seguinte (D-3).
    private static decimal IpcaFactor(
        decimal realRate, IReadOnlyList<DateOnly> businessDays, MarketData market,
        out bool isEstimated, out bool isProjected)
    {
        isEstimated = false;
        isProjected = false;
        var ipcaFactor = 1m;
        var firstOfCurrentMonth = new DateOnly(market.Today.Year, market.Today.Month, 1);
        decimal? projectedVariation = null;

        foreach (var month in businessDays.GroupBy(day => new DateOnly(day.Year, day.Month, 1)))
        {
            var ipca = market.GetIpca(month.Key);

            if (ipca is null)
            {
                var lastPublished = market.LastPublishedIpca
                    ?? throw new DomainException("Não há cotação de IPCA publicada para estimar a projeção.");

                // Mês anterior ao último publicado e sem cotação é buraco na série importada, não
                // variação ainda não divulgada: estimar esconderia a falha (como a RN-09 faz com o CDI).
                // Stryker disable once Equality : o mês do último IPCA publicado tem cotação, então GetIpca nunca devolve nulo para ele e month.Key == lastPublished.Month não chega a esta comparação
                if (month.Key < lastPublished.Month)
                {
                    throw new DomainException($"Cotação de IPCA ausente para {month.Key:yyyy-MM}.");
                }

                if (month.Key >= firstOfCurrentMonth)
                {
                    if (projectedVariation is null)
                    {
                        projectedVariation = market.TryGetProjectedIpca(out var variation, out var failure)
                            ? variation
                            : throw new DomainException(failure);
                    }

                    ipca = projectedVariation;
                    isProjected = true;
                }
                else
                {
                    ipca = lastPublished.Value;
                    isEstimated = true;
                }
            }

            var monthBusinessDays = market.Calendar.CountBusinessDays(month.Key, month.Key.AddMonths(1));

            ipcaFactor *= DecimalMath.Pow(1 + ipca.Value / 100, (decimal)month.Count() / monthBusinessDays);
        }

        return ipcaFactor * PreFixedFactor(realRate, businessDays.Count);
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
