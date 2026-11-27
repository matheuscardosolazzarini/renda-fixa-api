using System.Globalization;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.Entities;

// Projeção de IPCA+ com data futura (RN-12, D-3 a D-5). Valores esperados dos casos 6 e 6-bis da
// seção "Casos de referência" de docs/SPEC-F12-EXECUCAO.md. Feriados de 2026 que caem nos
// intervalos: 12/10, 02/11, 20/11 e 25/12.
public class PositionIpcaFutureProjectionTests
{
    // Janela real da série 433, 10/2025 a 09/2026. O IPCA de 10/2026 (0,14) não entra: em 05/11 ainda
    // não tinha sido divulgado.
    private const string RealWindow = "0.09 0.18 0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82";

    [Fact]
    public void Ipca_com_data_futura_estima_o_mes_encerrado_e_projeta_o_mes_de_hoje_e_os_seguintes()
    {
        // Arrange: caso 6. Hoje 05/11/2026. Outubro (21/21 DU) encerrado e não divulgado usa a
        // última variação, 0,82; novembro (19/19) e dezembro (10/22) usam a média geométrica
        // 0,37 (0,37357136% arredondado em 2 casas). Fator IPCA 1,0136305087556, fator real
        // 1,06^(50/252) = 1,0116283813225; bruto de 1.000 = 1.025,42; IR 22,5% sobre 25,42 = 5,72.
        var market = Create(new DateOnly(2026, 11, 5), IpcaSeries("2025-10-01", RealWindow));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        Assert.Equal(1_025.42m, projection.GrossAmount);
        Assert.Equal(25.42m, projection.GrossYield);
        Assert.Equal(0m, projection.IofRate);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(5.72m, projection.TaxAmount);
        Assert.Equal(1_019.70m, projection.NetAmount);
        Assert.Equal(75, projection.ElapsedDays);
        Assert.Equal(50, projection.BusinessDays);
        Assert.True(projection.IsEstimated);
        Assert.True(projection.IsProjected);
    }

    [Fact]
    public void Ipca_com_hoje_no_primeiro_dia_do_mes_seguinte_passa_o_mes_anterior_de_projetado_a_estimado()
    {
        // Arrange: caso 6-bis. Hoje 01/12/2026: novembro (encerrado, não divulgado) usa a última
        // variação 0,82 e só dezembro continua projetado (0,37). Fator IPCA
        // 1,0082² x 1,0016801243016 = 1,0181750313118; fator total 1,0300147588; bruto 1.030,01;
        // IR 6,75; líquido 1.023,26.
        var market = Create(new DateOnly(2026, 12, 1), IpcaSeries("2025-10-01", RealWindow));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        Assert.Equal(1_030.01m, projection.GrossAmount);
        Assert.Equal(6.75m, projection.TaxAmount);
        Assert.Equal(1_023.26m, projection.NetAmount);
        Assert.True(projection.IsEstimated);
        Assert.True(projection.IsProjected);
    }

    [Fact]
    public void Ipca_com_hoje_no_ultimo_dia_do_mes_ainda_projeta_o_mes_de_hoje()
    {
        // Arrange: caso 6-bis, fronteira de baixo. Hoje 30/11/2026: novembro ainda é o mês de hoje,
        // então o resultado é o do caso 6.
        var market = Create(new DateOnly(2026, 11, 30), IpcaSeries("2025-10-01", RealWindow));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        Assert.Equal(1_025.42m, projection.GrossAmount);
        Assert.Equal(1_019.70m, projection.NetAmount);
    }

    [Fact]
    public void Ipca_em_31_de_dezembro_projeta_dezembro()
    {
        // Arrange: virada de ano. Último publicado 11/2026 (janela 12/2025 a 11/2026); hoje
        // 31/12/2026, ainda dentro de dezembro, que não tem IPCA publicado.
        var market = Create(new DateOnly(2026, 12, 31), IpcaSeries("2025-12-01", WindowEndingNovember2026));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 12, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        Assert.True(projection.IsProjected);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Ipca_em_1_de_janeiro_estima_dezembro_em_vez_de_projetar()
    {
        // Arrange: virada de ano. Hoje 01/01/2027: dezembro/2026 está encerrado e não divulgado,
        // então usa a última variação e deixa de ser projetado.
        var market = Create(new DateOnly(2027, 1, 1), IpcaSeries("2025-12-01", WindowEndingNovember2026));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 12, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        Assert.True(projection.IsEstimated);
        Assert.False(projection.IsProjected);
    }

    [Fact]
    public void Ipca_projetado_com_11_meses_na_serie_lanca_excecao_com_a_contagem_e_o_primeiro_mes_ausente()
    {
        // Arrange: 11/2025 a 09/2026 são 11 meses; novembro/2026 precisa de projeção e a janela
        // é 10/2025 a 09/2026.
        var market = Create(
            new DateOnly(2026, 11, 5),
            IpcaSeries("2025-11-01", "0.18 0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82"));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "A projeção futura do IPCA exige os 12 meses de 2025-10 a 2026-09; há 11, falta 2025-10.",
            exception.Message);
    }

    [Fact]
    public void Ipca_projetado_com_12_meses_e_uma_lacuna_na_janela_lanca_excecao_com_o_mes_ausente()
    {
        // Arrange: 09/2025 a 09/2026 sem 01/2026. A janela de 10/2025 a 09/2026 tem 11.
        var rates = IpcaSeries("2025-09-01", "0.48 " + RealWindow)
            .Where(rate => rate.ReferenceDate != new DateOnly(2026, 1, 1));
        var market = Create(new DateOnly(2026, 11, 5), rates.ToArray());
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(
            "A projeção futura do IPCA exige os 12 meses de 2025-10 a 2026-09; há 11, falta 2026-01.",
            exception.Message);
    }

    [Fact]
    public void Ipca_sem_mes_projetado_no_intervalo_nao_exige_a_janela_de_12_meses()
    {
        // Arrange: só 09/2026 publicado; o intervalo de outubro (encerrado, não divulgado) é
        // estimado, e hoje 05/11 não deixa nenhum mês projetado dentro do intervalo.
        var market = Create(new DateOnly(2026, 11, 5), Ipca("2026-09-01", 0.82m));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 10, 15), market);

        // Assert
        Assert.True(projection.IsEstimated);
        Assert.False(projection.IsProjected);
    }

    [Fact]
    public void Ipca_publicado_no_mes_de_hoje_prevalece_sobre_a_projecao()
    {
        // Arrange: hoje 05/11/2026 e o IPCA de novembro (0,20) já publicado, só ele na série.
        // Novembro tem 19 DU; 03 a 09/11 são 5 (03, 04, 05, 06 e 09). Fator IPCA 1,002^(5/19) e
        // fator real 1,06^(5/252): bruto de 1.000 = 1.001,68.
        var market = Create(new DateOnly(2026, 11, 5), Ipca("2026-11-01", 0.20m));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 11, 3));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 11, 10), market);

        // Assert
        Assert.Equal(1_001.68m, projection.GrossAmount);
        Assert.Equal(5, projection.BusinessDays);
        Assert.False(projection.IsProjected);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Ipca_com_a_serie_vazia_lanca_excecao_sem_publicado_para_estimar()
    {
        // Arrange
        var market = Create(new DateOnly(2026, 11, 5));
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("Não há cotação de IPCA publicada para estimar a projeção.", exception.Message);
    }

    // 12/2025 a 11/2026: os 11 primeiros são os reais (12/2025 a 09/2026 mais 10/2026 = 0,14) e
    // novembro é fictício; a média não é conferida, só as marcas.
    private const string WindowEndingNovember2026 =
        "0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82 0.14 0.20";

    private static IndexRate[] IpcaSeries(string firstMonth, string values)
    {
        var start = DateOnly.Parse(firstMonth, CultureInfo.InvariantCulture);

        return values
            .Split(' ')
            .Select((value, index) => Ipca(
                start.AddMonths(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                decimal.Parse(value, CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static FixedIncomeAsset CriarAsset() =>
        new("CDB IPCA+", "Banco X", AssetType.Cdb, IndexType.Ipca, 6m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
}
