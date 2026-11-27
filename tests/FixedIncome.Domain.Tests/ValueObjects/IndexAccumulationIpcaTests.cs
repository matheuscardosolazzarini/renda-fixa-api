using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.ValueObjects;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.ValueObjects;

// Variação acumulada do IPCA (RN-13). Valores esperados dos casos 16, 17, 18 e 21 da seção "Casos de
// referência" de docs/SPEC-F12-EXECUCAO.md, conferidos na Calculadora do Cidadão. O fator é
// arredondado em 7 casas (D-10); com 8 casas, 4,264385 e 3,951317 divergem da calculadora.
public class IndexAccumulationIpcaTests
{
    // Série 433 do SGS de 01/2025 a 12/2025.
    private static IndexRate[] Serie2025() =>
    [
        Ipca("2025-01-01", 0.16m), Ipca("2025-02-01", 1.31m), Ipca("2025-03-01", 0.56m),
        Ipca("2025-04-01", 0.43m), Ipca("2025-05-01", 0.26m), Ipca("2025-06-01", 0.24m),
        Ipca("2025-07-01", 0.26m), Ipca("2025-08-01", -0.11m), Ipca("2025-09-01", 0.48m),
        Ipca("2025-10-01", 0.09m), Ipca("2025-11-01", 0.18m), Ipca("2025-12-01", 0.33m)
    ];

    // Série 433 de 01/2026 a 09/2026, com deflação em agosto.
    private static IndexRate[] Serie2026AteSetembro() =>
    [
        Ipca("2026-01-01", 0.33m), Ipca("2026-02-01", 0.70m), Ipca("2026-03-01", 0.88m),
        Ipca("2026-04-01", 0.67m), Ipca("2026-05-01", 0.58m), Ipca("2026-06-01", 0.16m),
        Ipca("2026-07-01", 0.07m), Ipca("2026-08-01", -0.32m), Ipca("2026-09-01", 0.82m)
    ];

    [Fact]
    public void Ipca_de_2025_com_from_e_to_no_meio_dos_meses_devolve_o_percentual_da_calculadora()
    {
        // Arrange: produto 1,0426438490457, fator em 7 casas 1,0426438.
        var market = Create(Serie2025());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2025, 1, 10), new DateOnly(2025, 12, 20));

        // Assert
        Assert.Equal(4.264380m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2025, 12, 20), result.EffectiveTo);
    }

    [Fact]
    public void Ipca_de_janeiro_a_setembro_de_2026_com_deflacao_em_agosto_devolve_o_percentual_da_calculadora()
    {
        // Arrange: produto 1,039513166123, fator em 7 casas 1,0395132.
        var market = Create(Serie2026AteSetembro());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 15), new DateOnly(2026, 9, 20));

        // Assert
        Assert.Equal(3.951320m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 9, 20), result.EffectiveTo);
    }

    [Fact]
    public void Ipca_com_to_no_ultimo_dia_do_ultimo_mes_publicado_nao_corta()
    {
        // Arrange: caso 17d.
        var market = Create(Serie2026AteSetembro());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 15), new DateOnly(2026, 9, 30));

        // Assert
        Assert.Equal(3.951320m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 9, 30), result.EffectiveTo);
    }

    [Fact]
    public void Ipca_com_to_depois_do_ultimo_mes_publicado_termina_no_ultimo_dia_dele()
    {
        // Arrange: caso 17a. O mês de to (11/2026) é posterior ao último publicado (09/2026).
        var market = Create(Serie2026AteSetembro());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 15), new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(3.951320m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 9, 30), result.EffectiveTo);
    }

    [Fact]
    public void Ipca_com_corte_inclui_o_ultimo_mes_publicado_quando_ele_e_outubro()
    {
        // Arrange: caso 17c. Com outubro (0,14) o produto é 1,0409684845557, fator 1,0409685.
        var market = Create(Serie2026AteSetembro().Append(Ipca("2026-10-01", 0.14m)).ToArray());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 15), new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(4.096850m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 10, 31), result.EffectiveTo);
    }

    [Fact]
    public void Ipca_com_from_no_meio_do_ultimo_mes_publicado_devolve_um_mes_e_effective_to_depois_de_from()
    {
        // Arrange: caso 17b.
        var market = Create(Serie2026AteSetembro());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2026, 9, 10), new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(0.82m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 9, 30), result.EffectiveTo);
        Assert.True(result.EffectiveTo > new DateOnly(2026, 9, 10));
    }

    [Fact]
    public void Chamar_de_novo_com_to_igual_ao_effective_to_devolve_o_mesmo_resultado()
    {
        // Arrange
        var market = Create(Serie2026AteSetembro());
        var first = IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 15), new DateOnly(2026, 11, 26));

        // Act
        var second = IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 15), first.EffectiveTo);

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void Ipca_com_from_igual_a_to_no_meio_de_um_mes_publicado_devolve_a_variacao_do_mes()
    {
        // Arrange: caso 18, agosto de 2026 com deflação de 0,32%.
        var market = Create(Serie2026AteSetembro());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 15));

        // Assert
        Assert.Equal(-0.32m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 8, 15), result.EffectiveTo);
    }

    [Fact]
    public void Mes_encerrado_e_nao_divulgado_nao_entra_no_acumulado_do_ipca()
    {
        // Arrange: caso 21. Hoje 05/11/2026, outubro está encerrado e sem IPCA; a projeção o
        // estimaria com a última variação, mas o acumulado só lê o que foi publicado.
        var market = Create(new DateOnly(2026, 11, 5), Serie2026AteSetembro());

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 20));

        // Assert
        Assert.Equal(0.82m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2026, 9, 30), result.EffectiveTo);
    }

    [Fact]
    public void Lacuna_de_ipca_antes_do_ultimo_mes_publicado_e_falha_de_importacao()
    {
        // Arrange: falta 02/2026, anterior ao último publicado (03/2026).
        var market = Create(Ipca("2026-01-01", 0.33m), Ipca("2026-03-01", 0.88m));

        // Act
        Action act = () => IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 20));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("Cotação de IPCA ausente para 2026-02.", exception.Message);
    }

    [Fact]
    public void Ipca_com_from_posterior_a_to_e_rejeitado()
    {
        // Arrange
        var market = Create(Serie2026AteSetembro());

        // Act
        Action act = () => IndexAccumulation.Ipca(market, new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 15));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("A data inicial não pode ser posterior à data final.", exception.Message);
    }

    [Fact]
    public void Ipca_com_mes_de_from_depois_do_ultimo_publicado_e_periodo_sem_cotacao()
    {
        // Arrange: o último publicado é 09/2026.
        var market = Create(Serie2026AteSetembro());

        // Act
        Action act = () => IndexAccumulation.Ipca(market, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "Não há cotação de IPCA publicada no período de 2026-10-01 a 2026-10-31.", exception.Message);
    }

    [Fact]
    public void Ipca_sem_nenhuma_cotacao_e_periodo_sem_cotacao()
    {
        // Arrange
        var market = Create();

        // Act
        Action act = () => IndexAccumulation.Ipca(market, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "Não há cotação de IPCA publicada no período de 2026-01-01 a 2026-03-31.", exception.Message);
    }

    [Fact]
    public void Ipca_nao_consulta_o_calendario_e_aceita_periodo_fora_da_cobertura_dele()
    {
        // Arrange: o calendário dos testes cobre 2020 a 2030; o IPCA de 2019 só depende dos meses.
        var market = Create(Ipca("2019-01-01", 0.32m));

        // Act
        var result = IndexAccumulation.Ipca(market, new DateOnly(2019, 1, 5), new DateOnly(2019, 1, 20));

        // Assert
        Assert.Equal(0.32m, result.AccumulatedPercentage);
        Assert.Equal(new DateOnly(2019, 1, 20), result.EffectiveTo);
    }
}
