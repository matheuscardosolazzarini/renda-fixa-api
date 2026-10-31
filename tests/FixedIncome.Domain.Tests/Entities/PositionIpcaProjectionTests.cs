using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.Entities;

// Projeção de títulos IPCA+ (RN-10), com o pro rata em dias úteis por mês civil do achado B3.
// Valores esperados do caso 6 da seção "Casos de referência" de docs/SPEC-F7-EXECUCAO.md,
// na convenção C6b. IPCA publicado (SGS 433) até 09/2026.
public class PositionIpcaProjectionTests
{
    private static readonly IndexRate[] PublishedIpca =
    [
        Ipca("2026-01-01", 0.33m),
        Ipca("2026-02-01", 0.70m),
        Ipca("2026-03-01", 0.88m),
        Ipca("2026-04-01", 0.67m),
        Ipca("2026-05-01", 0.58m),
        Ipca("2026-06-01", 0.16m),
        Ipca("2026-07-01", 0.07m),
        Ipca("2026-08-01", -0.32m),
        Ipca("2026-09-01", 0.82m)
    ];

    [Fact]
    public void Ipca_mais_taxa_em_meses_cheios_compoe_o_ipca_do_mes_com_a_taxa_real()
    {
        // Arrange: caso 6.1. Fator IPCA 1,0067 x 1,0058 x 1,0016 = 1,014158922176 e fator real
        // 1,06^(61/252) = 1,014204717056; total 1,028564762715. 91 dias corridos: IOF zero.
        var market = Create(PublishedIpca);
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 4, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 7, 1), market);

        // Assert
        Assert.Equal(1028.56m, projection.GrossAmount);
        Assert.Equal(28.56m, projection.GrossYield);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(6.43m, projection.TaxAmount);
        Assert.Equal(1022.13m, projection.NetAmount);
        Assert.Equal(61, projection.BusinessDays);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Ipca_em_mes_parcial_aplica_o_pro_rata_em_dias_uteis_do_mes_civil()
    {
        // Arrange: caso 6.2. Abril 11/20 (sem o feriado de 21/04), maio 20/20, junho 9/21 (sem
        // o feriado de 04/06): fator IPCA 1,010192716602; fator real 1,06^(40/252).
        var market = Create(PublishedIpca);
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 4, 15));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 6, 15), market);

        // Assert
        Assert.Equal(1019.58m, projection.GrossAmount);
        Assert.Equal(19.58m, projection.GrossYield);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(4.41m, projection.TaxAmount);
        Assert.Equal(1015.17m, projection.NetAmount);
        Assert.Equal(40, projection.BusinessDays);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Ipca_de_mes_sem_divulgacao_usa_o_ultimo_publicado_e_marca_a_projecao_como_estimada()
    {
        // Arrange: caso 6.3. Outubro (9/21 dias úteis) usa 0,82, o IPCA de setembro.
        // IR C6b: round2(18,78 x 22,5% = 4,2255) = 4,23.
        var market = Create(PublishedIpca);
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 10, 15), market);

        // Assert
        Assert.Equal(1018.78m, projection.GrossAmount);
        Assert.Equal(18.78m, projection.GrossYield);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(4.23m, projection.TaxAmount);
        Assert.Equal(1014.55m, projection.NetAmount);
        Assert.Equal(30, projection.BusinessDays);
        Assert.True(projection.IsEstimated);
    }

    [Fact]
    public void Ipca_com_deflacao_pode_render_menos_que_o_principal_e_nao_gera_imposto()
    {
        // Arrange: caso 6.4. Agosto de -0,32% com 21/21 dias úteis (01 e 02/08 são fim de
        // semana); o fator total 0,997626883592 dá rendimento de -2,37. Sem rendimento positivo,
        // IOF e IR são zero, mas as alíquotas seguem a tabela pelo prazo de 29 dias.
        var market = Create(PublishedIpca);
        var asset = CriarAsset(rate: 1m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 8, 3));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 9, 1), market);

        // Assert
        Assert.Equal(997.63m, projection.GrossAmount);
        Assert.Equal(-2.37m, projection.GrossYield);
        Assert.Equal(3m, projection.IofRate);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(997.63m, projection.NetAmount);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Ipca_ausente_no_meio_da_serie_lanca_excecao_citando_o_mes()
    {
        // Arrange: série publicada de 2026-01 a 2026-09 sem o IPCA de 2026-05. Maio é anterior ao
        // último publicado (setembro): é falha de importação, não variação ainda não divulgada.
        var rates = PublishedIpca.Where(r => r.ReferenceDate != new DateOnly(2026, 5, 1)).ToArray();
        var market = Create(rates);
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 4, 1));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 7, 1), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("2026-05", exception.Message);
    }

    [Fact]
    public void Ipca_ausente_no_primeiro_mes_do_intervalo_anterior_ao_ultimo_publicado_lanca_excecao()
    {
        // Arrange: série sem o IPCA de 2026-04, com o intervalo começando em abril.
        var rates = PublishedIpca.Where(r => r.ReferenceDate != new DateOnly(2026, 4, 1)).ToArray();
        var market = Create(rates);
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 4, 15));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 6, 15), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("2026-04", exception.Message);
    }

    [Fact]
    public void Ipca_do_mes_igual_ao_ultimo_publicado_usa_a_cotacao_sem_marcar_como_estimado()
    {
        // Arrange: setembro (0,82) é o último publicado; o intervalo termina em 15/09.
        var market = Create(PublishedIpca);
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 9, 15), market);

        // Assert
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Ipca_sem_nenhuma_cotacao_publicada_lanca_excecao()
    {
        // Arrange: sem IPCA algum não há o que estimar (RN-09 vale também para o IPCA).
        var market = Create();
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 4, 1));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 7, 1), market);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Ipca_sem_dia_util_no_intervalo_nao_exige_cotacao()
    {
        // Arrange: de sábado 12/09/2026 a segunda 14/09/2026 não há dia útil no intervalo.
        var market = Create();
        var asset = CriarAsset(rate: 6m);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 12));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 9, 14), market);

        // Assert
        Assert.Equal(1000.00m, projection.GrossAmount);
        Assert.Equal(0, projection.BusinessDays);
        Assert.False(projection.IsEstimated);
    }

    private static FixedIncomeAsset CriarAsset(decimal rate)
    {
        return new FixedIncomeAsset(
            "Título IPCA+", "Emissor Teste", AssetType.Cdb, IndexType.Ipca, rate,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
    }
}
