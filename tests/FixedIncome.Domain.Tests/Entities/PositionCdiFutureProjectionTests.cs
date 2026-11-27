using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.Entities;

// Projeção de CDI com data futura (RN-12). Valores esperados dos casos 1, 1-bis, 2, 3, 3-bis e
// 3-ter da seção "Casos de referência" de docs/SPEC-F12-EXECUCAO.md. Série 12 real cortada em
// 19/11/2026, a última cotação do banco no stub; 20/11/2026 é feriado.
public class PositionCdiFutureProjectionTests
{
    private static readonly DateOnly ApplicationDate = new(2026, 11, 16);
    private static readonly DateOnly Reference = new(2026, 11, 26);

    [Fact]
    public void Cdi_com_data_futura_usa_a_ultima_cotacao_nos_dias_uteis_sem_cotacao_e_marca_como_projetado()
    {
        // Arrange: caso 1. Hoje 19/11, dia útil anterior 18/11; 23, 24 e 25/11 ficam depois dele e
        // são projetados com 0,050788. 7 DU (4 publicados + 3 projetados), 10 dias corridos, fator
        // 1,00356058, IOF de 66% sobre 3,56 = 2,35, IR 22,5% sobre 1,21 = 0,27.
        var market = Create(new DateOnly(2026, 11, 19), SerieAte19());
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, Reference, market);

        // Assert
        Assert.Equal(1_003.56m, projection.GrossAmount);
        Assert.Equal(3.56m, projection.GrossYield);
        Assert.Equal(66m, projection.IofRate);
        Assert.Equal(2.35m, projection.IofAmount);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(0.27m, projection.TaxAmount);
        Assert.Equal(1_000.94m, projection.NetAmount);
        Assert.Equal(10, projection.ElapsedDays);
        Assert.Equal(7, projection.BusinessDays);
        Assert.True(projection.IsProjected);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Cdi_projetado_usa_o_valor_da_ultima_cotacao_e_nao_o_de_dias_anteriores()
    {
        // Arrange: caso 1-bis. Igual ao caso 1, com 19/11 em 0,051660: 16, 17 e 18 a 0,050788 e os
        // dias 19, 23, 24 e 25 a 0,051660; fator 1,00359557. Com 0,050788 nos dias projetados o
        // bruto seria 1.003,56.
        var rates = new[]
        {
            Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m),
            Cdi("2026-11-18", 0.050788m), Cdi("2026-11-19", 0.051660m)
        };
        var market = Create(new DateOnly(2026, 11, 19), rates);
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, Reference, market);

        // Assert
        Assert.Equal(1_003.60m, projection.GrossAmount);
        Assert.Equal(2.38m, projection.IofAmount);
        Assert.Equal(0.27m, projection.TaxAmount);
        Assert.Equal(1_000.95m, projection.NetAmount);
        Assert.True(projection.IsProjected);
    }

    [Fact]
    public void Cdi_projetado_com_110_por_cento_aplica_o_percentual_ao_valor_da_ultima_cotacao()
    {
        // Arrange: caso 2. Fator 1,00391724, IOF de 66% sobre 3,92 = 2,59, IR 22,5% sobre 1,33 = 0,30.
        var market = Create(new DateOnly(2026, 11, 19), SerieAte19());
        var asset = CriarAsset(110);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, Reference, market);

        // Assert
        Assert.Equal(1_003.92m, projection.GrossAmount);
        Assert.Equal(2.59m, projection.IofAmount);
        Assert.Equal(0.30m, projection.TaxAmount);
        Assert.Equal(1_001.03m, projection.NetAmount);
        Assert.True(projection.IsProjected);
    }

    [Fact]
    public void Cdi_com_referencia_no_dia_util_seguinte_a_ultima_cotacao_nao_projeta_nada()
    {
        // Arrange: caso 3a. Última cotação 19/11 (20/11 feriado); a referência 23/11 é o dia útil
        // seguinte, a data padrão, e o fim do intervalo é exclusivo: só 16 a 19/11, 4 DU, 7 dias.
        var market = Create(new DateOnly(2026, 11, 19), SerieAte19());
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 11, 23), market);

        // Assert
        Assert.Equal(1_002.03m, projection.GrossAmount);
        Assert.Equal(76m, projection.IofRate);
        Assert.Equal(1.54m, projection.IofAmount);
        Assert.Equal(0.11m, projection.TaxAmount);
        Assert.Equal(1_000.38m, projection.NetAmount);
        Assert.Equal(4, projection.BusinessDays);
        Assert.False(projection.IsProjected);
    }

    [Fact]
    public void Cdi_com_referencia_um_dia_util_depois_projeta_so_o_dia_da_data_padrao()
    {
        // Arrange: caso 3b. Referência 24/11: projeta só 23/11. 5 DU, 8 dias, fator 1,00254198,
        // IOF de 73% sobre 2,54 = 1,85, IR 22,5% sobre 0,69 = 0,16.
        var market = Create(new DateOnly(2026, 11, 19), SerieAte19());
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 11, 24), market);

        // Assert
        Assert.Equal(1_002.54m, projection.GrossAmount);
        Assert.Equal(73m, projection.IofRate);
        Assert.Equal(1.85m, projection.IofAmount);
        Assert.Equal(0.16m, projection.TaxAmount);
        Assert.Equal(1_000.53m, projection.NetAmount);
        Assert.Equal(5, projection.BusinessDays);
        Assert.True(projection.IsProjected);
    }

    // Caso 3-bis: mesma série e mesmo título do caso 1, referência 26/11. Dias úteis sem cotação
    // 23, 24 e 25/11; a falha é o primeiro dia com NextBusinessDay(dia) < today.
    [Theory]
    [InlineData("2026-11-26")] // dia útil anterior 25/11: 23 e 24 falham
    [InlineData("2026-11-25")] // dia útil anterior 24/11: 23 falha
    public void Cdi_com_dia_sem_cotacao_antes_do_dia_util_anterior_a_hoje_lanca_excecao_com_a_primeira_data(
        string today)
    {
        // Arrange
        var market = Create(DateOnly.Parse(today), SerieAte19());
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var act = () => position.Project(asset, Reference, market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("Cotação de CDI ausente para 2026-11-23.", exception.Message);
    }

    [Theory]
    [InlineData("2026-11-24")] // dia útil anterior 23/11
    [InlineData("2026-11-23")] // dia útil anterior 19/11 (20/11 feriado, 21 e 22 fim de semana)
    public void Cdi_com_todos_os_dias_sem_cotacao_a_partir_do_dia_util_anterior_a_hoje_projeta(string today)
    {
        // Arrange
        var market = Create(DateOnly.Parse(today), SerieAte19());
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, Reference, market);

        // Assert: igual ao caso 1.
        Assert.Equal(1_003.56m, projection.GrossAmount);
        Assert.Equal(1_000.94m, projection.NetAmount);
        Assert.True(projection.IsProjected);
    }

    [Theory]
    [InlineData("2026-11-21")] // sábado
    [InlineData("2026-11-20")] // feriado
    public void Cdi_com_hoje_em_dia_nao_util_parte_do_dia_util_anterior_e_projeta(string today)
    {
        // Arrange: caso 3-ter. Última cotação 18/11; o dia útil anterior a 20/11 e a 21/11 é 19/11,
        // então 19, 23, 24 e 25/11 são projetados com 0,050788 (mesmo resultado do caso 1). Uma
        // implementação que partisse do último dia útil igual ou anterior a hoje cairia em 18/11.
        var rates = new[] { Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m), Cdi("2026-11-18", 0.050788m) };
        var market = Create(DateOnly.Parse(today), rates);
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, Reference, market);

        // Assert
        Assert.Equal(1_003.56m, projection.GrossAmount);
        Assert.Equal(2.35m, projection.IofAmount);
        Assert.Equal(0.27m, projection.TaxAmount);
        Assert.Equal(1_000.94m, projection.NetAmount);
        Assert.True(projection.IsProjected);
    }

    [Fact]
    public void Cdi_com_hoje_em_sabado_e_ultima_cotacao_um_dia_util_antes_lanca_excecao_no_dia_sem_cotacao()
    {
        // Arrange: caso 3-ter. Última cotação 17/11 e hoje sábado 21/11: o dia útil anterior é
        // 19/11, e 18/11 fica antes dele.
        var rates = new[] { Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m) };
        var market = Create(new DateOnly(2026, 11, 21), rates);
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var act = () => position.Project(asset, Reference, market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("Cotação de CDI ausente para 2026-11-18.", exception.Message);
    }

    [Fact]
    public void Cdi_com_lacuna_antes_da_ultima_cotacao_lanca_excecao_mesmo_com_o_periodo_terminando_logo_depois()
    {
        // Arrange: cenário da D-1. 18/11 não tem cotação e a última é 19/11; a lacuna é anterior à
        // última cotação, então nunca vira projeção, qualquer que seja hoje.
        var rates = new[] { Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m), Cdi("2026-11-19", 0.050788m) };
        var market = Create(new DateOnly(2026, 11, 19), rates);
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 11, 23), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("Cotação de CDI ausente para 2026-11-18.", exception.Message);
    }

    [Fact]
    public void Cdi_sem_nenhuma_cotacao_lanca_excecao_mesmo_com_data_futura()
    {
        // Arrange
        var market = Create(new DateOnly(2026, 11, 19));
        var asset = CriarAsset(100);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var act = () => position.Project(asset, Reference, market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("Cotação de CDI ausente para 2026-11-16.", exception.Message);
    }

    [Fact]
    public void Vencimento_antes_do_primeiro_dia_sem_cotacao_nao_projeta_mesmo_com_referencia_depois()
    {
        // Arrange: vencimento 23/11 (RN-02) limita o intervalo a 16 a 19/11, todos publicados.
        var market = Create(new DateOnly(2026, 11, 19), SerieAte19());
        var asset = new FixedIncomeAsset(
            "CDB CDI", "Banco X", AssetType.Cdb, IndexType.Cdi, 100m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 23));
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 11, 30), market);

        // Assert
        Assert.False(projection.IsProjected);
        Assert.Equal(1_002.03m, projection.GrossAmount);
        Assert.Equal(4, projection.BusinessDays);
    }

    [Fact]
    public void Prefixado_com_data_futura_nunca_e_projetado()
    {
        // Arrange: a taxa é contratada; hoje não muda nada.
        var market = Create(new DateOnly(2026, 11, 19));
        var asset = new FixedIncomeAsset(
            "CDB Pré", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, Reference, market);

        // Assert
        Assert.False(projection.IsProjected);
        Assert.False(projection.IsEstimated);
    }

    [Theory]
    [InlineData(IndexType.PreFixed)]
    [InlineData(IndexType.Cdi)]
    [InlineData(IndexType.Ipca)]
    public void Data_posterior_a_cobertura_do_calendario_continua_lancando_excecao_do_calendario(
        IndexType indexType)
    {
        // Arrange: o calendário de teste cobre até 31/12/2030; o título vence em 2035.
        var market = Create(new DateOnly(2026, 11, 19), SerieAte19());
        var asset = new FixedIncomeAsset(
            "Título", "Banco X", AssetType.Cdb, indexType, 10m,
            new DateOnly(2026, 1, 1), new DateOnly(2035, 1, 1));
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var act = () => position.Project(asset, new DateOnly(2031, 1, 5), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("fora do período coberto pelo calendário", exception.Message);
    }

    private static IndexRate[] SerieAte19() =>
    [
        Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m),
        Cdi("2026-11-18", 0.050788m), Cdi("2026-11-19", 0.050788m)
    ];

    private static FixedIncomeAsset CriarAsset(int percentOfCdi) =>
        new("CDB CDI", "Banco X", AssetType.Cdb, IndexType.Cdi, percentOfCdi,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
}
