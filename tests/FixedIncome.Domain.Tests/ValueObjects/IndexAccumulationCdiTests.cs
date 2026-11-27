using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.ValueObjects;

// Variação acumulada do CDI (RN-13). Valores esperados dos casos 14 e 15 da seção "Casos de
// referência" de docs/SPEC-F12-EXECUCAO.md, calculados à mão e conferidos na Calculadora do Cidadão.
public class IndexAccumulationCdiTests
{
    // Caso 14 (caso 4 da SPEC-F7): 0,051660 de 01/09 a 16/09 e 0,050788 de 17/09 a 30/09; 07/09 é
    // feriado; 21 dias úteis.
    private static IndexRate[] SerieDeSetembro() =>
    [
        Cdi("2026-09-01", 0.051660m), Cdi("2026-09-02", 0.051660m), Cdi("2026-09-03", 0.051660m),
        Cdi("2026-09-04", 0.051660m), Cdi("2026-09-08", 0.051660m), Cdi("2026-09-09", 0.051660m),
        Cdi("2026-09-10", 0.051660m), Cdi("2026-09-11", 0.051660m), Cdi("2026-09-14", 0.051660m),
        Cdi("2026-09-15", 0.051660m), Cdi("2026-09-16", 0.051660m),
        Cdi("2026-09-17", 0.050788m), Cdi("2026-09-18", 0.050788m), Cdi("2026-09-21", 0.050788m),
        Cdi("2026-09-22", 0.050788m), Cdi("2026-09-23", 0.050788m), Cdi("2026-09-24", 0.050788m),
        Cdi("2026-09-25", 0.050788m), Cdi("2026-09-28", 0.050788m), Cdi("2026-09-29", 0.050788m),
        Cdi("2026-09-30", 0.050788m)
    ];

    // Casos 1 a 3: última cotação do banco em 19/11/2026; o dia útil seguinte é 23/11 (20/11 feriado).
    private static IndexRate[] SerieAte19DeNovembro() =>
    [
        Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m),
        Cdi("2026-11-18", 0.050788m), Cdi("2026-11-19", 0.050788m)
    ];

    [Fact]
    public void Periodo_de_setembro_devolve_o_percentual_da_calculadora_do_cidadao()
    {
        // Arrange: acumulado truncado 1,0108167257286023, fator 1,01081673.
        var market = Create(SerieDeSetembro());

        // Act
        var result = IndexAccumulation.Cdi(market, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));

        // Assert
        Assert.Equal(1.081673m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 10, 1), result.EffectiveTo);
        Assert.Equal(1_010.82m, Math.Round(1_000m * (1 + result.AccumulatedPercentage / 100), 2));
    }

    [Fact]
    public void Cotacao_do_proprio_dia_final_no_banco_nao_entra_porque_o_fim_e_exclusivo()
    {
        // Arrange: com a cotação de 01/10 (dia do fim exclusivo) o resultado não muda.
        var rates = SerieDeSetembro().Append(Cdi("2026-10-01", 0.050788m)).ToArray();
        var market = Create(rates);

        // Act
        var result = IndexAccumulation.Cdi(market, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));

        // Assert
        Assert.Equal(1.081673m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 10, 1), result.EffectiveTo);
    }

    [Fact]
    public void Acumulado_de_um_milhar_e_igual_ao_bruto_de_um_cdb_100_por_cento_do_cdi_nas_mesmas_datas()
    {
        // Arrange: prova da D-7, o produtório do CDI é um só. Caso 14: bruto 1.010,82.
        var market = Create(SerieDeSetembro());
        var asset = new FixedIncomeAsset(
            "CDB CDI", "Banco X", AssetType.Cdb, IndexType.Cdi, 100m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2026, 9, 1));

        // Act
        var accumulated = IndexAccumulation.Cdi(market, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));
        var projection = position.Project(asset, new DateOnly(2026, 10, 1), market);

        // Assert
        var accumulatedAmount = Math.Round(1_000m * (1 + accumulated.AccumulatedPercentage / 100), 2);
        Assert.Equal(1_010.82m, projection.GrossAmount);
        Assert.Equal(projection.GrossAmount, accumulatedAmount);
        Assert.False(projection.IsProjected);
    }

    [Theory]
    [InlineData("2026-11-26", "2026-11-23")] // corte: o dia útil seguinte à última cotação
    [InlineData("2026-11-24", "2026-11-23")] // corte, por um dia
    [InlineData("2026-11-23", "2026-11-23")] // exatamente o dia útil seguinte: sem corte
    [InlineData("2026-11-21", "2026-11-21")] // sábado antes do dia útil seguinte: sem corte
    [InlineData("2035-01-02", "2026-11-23")] // depois da cobertura do calendário: corta, sem erro
    public void Cdi_com_to_depois_da_ultima_cotacao_termina_no_dia_util_seguinte_a_ela_sem_projetar(
        string to, string expectedEffectiveTo)
    {
        // Arrange: dias usados 16, 17, 18 e 19/11, fator 1,00203307. Se projetasse 23, 24 e 25/11,
        // o percentual seria 0,356058.
        var market = Create(SerieAte19DeNovembro());

        // Act
        var result = IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 16), DateOnly.Parse(to));

        // Assert
        Assert.Equal(0.203307m, result.AccumulatedPercentage);
        Assert.Equal(DateOnly.Parse(expectedEffectiveTo), result.EffectiveTo);
    }

    [Fact]
    public void Chamar_de_novo_com_to_igual_ao_effective_to_devolve_o_mesmo_resultado()
    {
        // Arrange
        var market = Create(SerieAte19DeNovembro());
        var first = IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 26));

        // Act
        var second = IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 16), first.EffectiveTo);

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void Lacuna_de_cdi_antes_da_ultima_cotacao_e_falha_de_importacao()
    {
        // Arrange: falta 18/11, que é anterior à última cotação (19/11).
        var market = Create(
            Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m), Cdi("2026-11-19", 0.050788m));

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 26));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("Cotação de CDI ausente para 2026-11-18.", exception.Message);
    }

    [Fact]
    public void Cdi_com_from_posterior_a_to_e_rejeitado()
    {
        // Arrange
        var market = Create(SerieAte19DeNovembro());

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 17));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("A data inicial não pode ser posterior à data final.", exception.Message);
    }

    [Fact]
    public void Cdi_com_from_depois_da_ultima_cotacao_e_periodo_sem_cotacao()
    {
        // Arrange: from 23/11 é igual ao fim efetivo (23/11), então não sobra dia útil.
        var market = Create(SerieAte19DeNovembro());

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 26));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "Não há cotação de CDI publicada no período de 2026-11-23 a 2026-11-26.", exception.Message);
    }

    [Fact]
    public void Cdi_com_from_igual_a_to_num_dia_com_cotacao_explica_que_o_periodo_esta_vazio()
    {
        // Arrange: 17/11 tem cotação, mas o fim é exclusivo, então [17/11, 17/11) é vazio (caso A04 do QA).
        var market = Create(SerieAte19DeNovembro());

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 17));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "O período de 2026-11-17 a 2026-11-17 está vazio: no CDI a data final não entra no "
            + "acumulado, então to precisa ser posterior a from.",
            exception.Message);
    }

    [Fact]
    public void Cdi_com_from_igual_a_to_sem_nenhuma_cotacao_tambem_explica_que_o_periodo_esta_vazio()
    {
        // Arrange: o período vazio é erro da entrada, conferido antes de olhar a série.
        var market = Create();

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 17));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.StartsWith("O período de 2026-11-17 a 2026-11-17 está vazio", exception.Message);
    }

    [Fact]
    public void Cdi_em_fim_de_semana_e_periodo_sem_cotacao()
    {
        // Arrange: 21 e 22/11/2026 são sábado e domingo.
        var market = Create(SerieAte19DeNovembro());

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 22));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "Não há cotação de CDI publicada no período de 2026-11-21 a 2026-11-22.", exception.Message);
    }

    [Fact]
    public void Cdi_sem_nenhuma_cotacao_e_periodo_sem_cotacao()
    {
        // Arrange
        var market = Create();

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 19));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "Não há cotação de CDI publicada no período de 2026-11-16 a 2026-11-19.", exception.Message);
    }

    [Fact]
    public void Cdi_com_from_fora_da_cobertura_do_calendario_e_rejeitado()
    {
        // Arrange: o calendário dos testes cobre 2020 a 2030.
        var market = Create(SerieAte19DeNovembro());

        // Act
        Action act = () => IndexAccumulation.Cdi(market, new DateOnly(2019, 12, 2), new DateOnly(2026, 11, 19));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("fora do período coberto pelo calendário", exception.Message);
    }
}
