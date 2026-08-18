using FixedIncome.Domain.Common;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Entities;

public class Position : BaseEntity
{
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

    public PositionProjection Project(FixedIncomeAsset asset, DateOnly referenceDate, IndexRates rates)
    {
        if (asset.Id != AssetId)
        {
            throw new DomainException("O título informado não corresponde a esta posição.");
        }

        if (referenceDate < ApplicationDate)
        {
            throw new DomainException("A data de referência não pode ser anterior à data de aporte.");
        }

        // RN-02: a data de referência é limitada ao vencimento do título.
        var calculationDate = referenceDate > asset.MaturityDate ? asset.MaturityDate : referenceDate;
        var elapsedDays = calculationDate.DayNumber - ApplicationDate.DayNumber;

        var annualRate = asset.ResolveAnnualRate(rates);

        // RN-01: capitalização composta com base 365. Math.Pow opera em double; a
        // conversão é feita apenas na exponenciação porque a perda de precisão é
        // irrelevante na escala de valores tratada, e implementar potência em decimal
        // acrescentaria complexidade sem ganho prático.
        var growthFactor = (decimal)Math.Pow((double)(1 + annualRate / 100), (double)elapsedDays / 365);
        var grossAmount = InvestedAmount * growthFactor;
        var grossYield = grossAmount - InvestedAmount;

        // RN-03: o imposto incide apenas sobre o rendimento, nunca sobre o principal.
        var taxRate = IncomeTaxTable.RateFor(asset.AssetType, elapsedDays);
        var taxAmount = grossYield * (taxRate / 100);
        var netAmount = grossAmount - taxAmount;

        return new PositionProjection(
            InvestedAmount: Math.Round(InvestedAmount, 2, MidpointRounding.AwayFromZero),
            GrossAmount: Math.Round(grossAmount, 2, MidpointRounding.AwayFromZero),
            GrossYield: Math.Round(grossYield, 2, MidpointRounding.AwayFromZero),
            TaxRate: Math.Round(taxRate, 1, MidpointRounding.AwayFromZero),
            TaxAmount: Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero),
            NetAmount: Math.Round(netAmount, 2, MidpointRounding.AwayFromZero),
            ElapsedDays: elapsedDays);
    }
}
