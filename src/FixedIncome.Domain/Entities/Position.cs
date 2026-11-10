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

    private const decimal CdiScale = 10_000_000_000_000_000m; // 16 casas

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
        var growthFactor = asset.IndexType switch
        {
            IndexType.PreFixed => PreFixedFactor(asset.Rate, businessDays.Count),
            IndexType.Cdi => CdiFactor(asset.Rate, businessDays, market),
            IndexType.Ipca => IpcaFactor(asset.Rate, businessDays, market, out isEstimated),
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
            IsEstimated: isEstimated);
    }

    // RN-01: capitalização composta em base 252 dias úteis.
    private static decimal PreFixedFactor(decimal annualRate, int businessDays)
    {
        return DecimalMath.Pow(1 + annualRate / 100, (decimal)businessDays / 252);
    }

    // RN-09: produtório diário, sem fallback para cotação ausente. Metodologia do caderno de
    // fórmulas da B3: taxa diária com 8 casas (série 12 / 100), acumulado truncado em 16
    // casas a cada dia e fator final arredondado em 8 casas.
    private static decimal CdiFactor(decimal percentOfCdi, IReadOnlyList<DateOnly> businessDays, MarketData market)
    {
        var accumulated = 1m;

        foreach (var day in businessDays)
        {
            var cdi = market.GetCdi(day)
                ?? throw new DomainException($"Cotação de CDI ausente para {day:yyyy-MM-dd}.");

            var dailyFactor = 1 + cdi / 100 * percentOfCdi / 100;
            accumulated = decimal.Truncate(accumulated * dailyFactor * CdiScale) / CdiScale;
        }

        return Math.Round(accumulated, 8, MidpointRounding.AwayFromZero);
    }

    // RN-10: IPCA pro rata em dias úteis por mês civil (simplificação do achado B3: a
    // convenção de mercado usa a data de aniversário), vezes a taxa real em base 252. Mês
    // posterior ao último publicado usa a última variação e marca a projeção como estimada.
    private static decimal IpcaFactor(
        decimal realRate, IReadOnlyList<DateOnly> businessDays, MarketData market, out bool isEstimated)
    {
        isEstimated = false;
        var ipcaFactor = 1m;

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

                ipca = lastPublished.Value;
                isEstimated = true;
            }

            var monthBusinessDays = market.Calendar.CountBusinessDays(month.Key, month.Key.AddMonths(1));

            ipcaFactor *= DecimalMath.Pow(1 + ipca.Value / 100, (decimal)month.Count() / monthBusinessDays);
        }

        return ipcaFactor * PreFixedFactor(realRate, businessDays.Count);
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
