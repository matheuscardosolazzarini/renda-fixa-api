using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Application.Tests.TestData;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetPositionProjectionUseCaseTests
{
    // Relógio bem distante das datas dos testes: se o caso de uso usasse a data de hoje onde deve
    // usar a última cotação de CDI, os valores esperados não bateriam.
    private static readonly DateTimeOffset Now = new(2025, 12, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Projecao_valida_calcula_valores_liquidos_e_retorna_200()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert: caso 1.3 da spec (2025 tem 252 dias úteis, então o fator é 1,10 exato).
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(365, response.Data!.ElapsedDays);
        Assert.Equal(252, response.Data!.BusinessDays);
        Assert.Equal(1_100.00m, response.Data!.GrossAmount, precision: 2);
        Assert.Equal(100.00m, response.Data!.GrossYield, precision: 2);
        Assert.Equal(17.5m, response.Data!.TaxRate, precision: 1);
        Assert.Equal(17.50m, response.Data!.TaxAmount, precision: 2);
        Assert.Equal(1_082.50m, response.Data!.NetAmount, precision: 2);
    }

    [Fact]
    public async Task Projecao_valida_devolve_os_campos_de_iof_e_estimativa()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert: 365 dias corridos não pagam IOF; título pré-fixado nunca é estimado.
        Assert.Equal(0m, response.Data!.IofRate);
        Assert.Equal(0m, response.Data!.IofAmount);
        Assert.False(response.Data!.IsEstimated);
    }

    [Fact]
    public async Task Projecao_com_prazo_curto_devolve_iof_antes_do_ir()
    {
        // Arrange: PreFixed 10% a.a., 2025-04-01 a 2025-04-22. Derivação do esperado:
        // - 13 dias úteis (18/04 e 21/04 são feriados), fator 1,10^(13/252) = 1,0049289,
        //   bruto 1.000 x 1,0049289 = 1.004,93 e rendimento 4,93;
        // - 21 dias corridos: IOF de 30% sobre o rendimento = 1,479, arredondado para 1,48;
        // - IR de 22,5% sobre o rendimento menos o IOF: 22,5% de (4,93 - 1,48 = 3,45) = 0,77625,
        //   arredondado para 0,78; líquido 1.004,93 - 1,48 - 0,78 = 1.002,67.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(21, response.Data!.ElapsedDays);
        Assert.Equal(13, response.Data!.BusinessDays);
        Assert.Equal(1_004.93m, response.Data!.GrossAmount);
        Assert.Equal(30m, response.Data!.IofRate);
        Assert.Equal(1.48m, response.Data!.IofAmount);
        Assert.Equal(0.78m, response.Data!.TaxAmount);
        Assert.Equal(1_002.67m, response.Data!.NetAmount);
    }

    [Fact]
    public async Task Projecao_devolve_o_net_yield_como_liquido_menos_investido()
    {
        // Arrange: mesmo caso do teste anterior (caso 1a da spec da F13): líquido 1.002,67 sobre 1.000.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2.67m, response.Data!.NetYield);
        Assert.Equal(response.Data!.NetAmount - response.Data!.InvestedAmount, response.Data!.NetYield);
    }

    [Fact]
    public async Task Posicao_inexistente_retorna_404()
    {
        // Arrange
        var useCase = CriarCaso(position: null, asset: null);

        // Act
        var response = await useCase.ExecuteAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1));

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_vinculado_a_posicao_nao_encontrado_retorna_404()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(position, asset: null);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Data_de_referencia_anterior_ao_aporte_retorna_400()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 1, 1));

        // Assert
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Data_de_referencia_fora_da_cobertura_do_calendario_retorna_400()
    {
        // Arrange: o calendário de teste cobre até 2030-12-31 e o título vence em 2035.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2035, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2031, 6, 1));

        // Assert
        Assert.Equal(400, response.StatusCode);
        // B-05: cita a data enviada (e não a véspera) e o período coberto inteiro.
        Assert.Equal(
            "Data 2031-06-01 fora do período coberto pelo calendário (2025-01-01 a 2030-12-31).",
            response.Message);
    }

    [Fact]
    public async Task Cotacao_de_cdi_ausente_retorna_400_com_a_primeira_data_faltante()
    {
        // Arrange: aporte numa terça (2025-04-01); só há CDI de 01/04. Faltam 02/04 e 03/04.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.Cdi, 100m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 1), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 4));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("2025-04-02", response.Message);
    }

    [Fact]
    public async Task Cotacao_de_cdi_ausente_lembra_que_a_importacao_pode_estar_desligada()
    {
        // Arrange: aporte numa terça (2025-04-01); só há CDI de 01/04, falta 02/04.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.Cdi, 100m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 1), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 4));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "Cotação de CDI ausente para 2025-04-02. "
            + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.",
            response.Message);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_usa_o_dia_util_seguinte_a_ultima_cotacao_de_cdi()
    {
        // Arrange: a última cotação de CDI é quinta, 2025-04-17; 18/04 (Paixão), 19 e 20/04 (fim de
        // semana) e 21/04 (Tiradentes) não são úteis, então a data padrão é terça, 2025-04-22.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert: mesmos valores do caso de 13 dias úteis e 21 dias corridos (ver teste de IOF acima).
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(21, response.Data!.ElapsedDays);
        Assert.Equal(13, response.Data!.BusinessDays);
        Assert.Equal(1_004.93m, response.Data!.GrossAmount);
        Assert.Equal(1_002.67m, response.Data!.NetAmount);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_com_ultima_cotacao_de_cdi_na_sexta_usa_a_segunda()
    {
        // Arrange: sexta 2025-04-04 -> segunda 2025-04-07. Aporte em 2025-04-01: 6 dias corridos
        // e 4 dias úteis (02, 03, 04 e 07/04).
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 4), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(6, response.Data!.ElapsedDays);
        Assert.Equal(4, response.Data!.BusinessDays);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_e_sem_nenhuma_cotacao_de_cdi_usa_a_data_de_hoje_do_relogio()
    {
        // Arrange: sem CDI importado vale a data do TimeProvider, fixada em 2025-04-22.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var clock = new TimeProviderStub(new DateTimeOffset(2025, 4, 22, 12, 0, 0, TimeSpan.Zero));
        var useCase = CriarCaso(position, asset, clock);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(21, response.Data!.ElapsedDays);
        Assert.Equal(1_004.93m, response.Data!.GrossAmount);
    }

    // R-01: caso do QA. CDB pré-fixado de 12% a.a., R$ 1.000 aportados em 2026-10-28, tabela de
    // CDI vazia. Às 22h37 de 30/10 em Brasília (01h37 UTC de 31/10) a data de hoje é 2026-10-30:
    // 2 dias corridos e 2 úteis (28 e 29/10), IOF de 93%, bruto 1.000,90, IOF 0,84, IR 0,01 e
    // líquido 1.000,05 (valores conferidos no QA com referenceDate=2026-10-30).
    [Fact]
    public async Task Sem_data_e_sem_cotacao_de_cdi_entre_21h_e_meia_noite_em_brasilia_usa_a_data_de_brasilia()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "QA-R01", "QA-Banco", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31));
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 28));
        var clock = new TimeProviderStub(new DateTimeOffset(2026, 10, 31, 1, 37, 0, TimeSpan.Zero));
        var useCase = CriarCaso(position, asset, clock);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, response.Data!.ElapsedDays);
        Assert.Equal(2, response.Data!.BusinessDays);
        Assert.Equal(93m, response.Data!.IofRate);
        Assert.Equal(1_000.90m, response.Data!.GrossAmount);
        Assert.Equal(0.84m, response.Data!.IofAmount);
        Assert.Equal(0.01m, response.Data!.TaxAmount);
        Assert.Equal(1_000.05m, response.Data!.NetAmount);
    }

    // A janela UTC diferente de Brasília fecha às 03h UTC: a partir daí as datas coincidem.
    [Theory]
    [InlineData("2026-10-31T02:59:59Z", 2)]
    [InlineData("2026-10-31T03:00:00Z", 3)]
    public async Task Data_de_hoje_muda_para_o_dia_seguinte_so_a_partir_de_meia_noite_em_brasilia(
        string utcNow, int expectedElapsedDays)
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "QA-R01", "QA-Banco", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31));
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 28));
        var clock = new TimeProviderStub(DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture));
        var useCase = CriarCaso(position, asset, clock);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(expectedElapsedDays, response.Data!.ElapsedDays);
    }

    // B-03: com a data omitida, a mensagem explica de onde veio a data padrão.
    [Fact]
    public async Task Sem_data_com_data_padrao_da_ultima_cotacao_anterior_ao_aporte_informa_a_data_padrao_usada()
    {
        // Arrange: última cotação de CDI em quinta, 2026-10-29, então a data padrão é sexta, 2026-10-30.
        var asset = new FixedIncomeAsset(
            "QA-06", "QA-Banco", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31));
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 31));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2026, 10, 29), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "A data de referência padrão (2026-10-30, o dia útil seguinte à última cotação de CDI) "
            + "é anterior à data de aporte (2026-10-31). Informe referenceDate.",
            response.Message);
    }

    [Fact]
    public async Task Sem_data_e_sem_cotacao_com_data_de_hoje_anterior_ao_aporte_informa_a_data_de_hoje_usada()
    {
        // Arrange: sem CDI, a data padrão é a de hoje em Brasília (2026-10-30 às 22h37).
        var asset = new FixedIncomeAsset(
            "QA-06", "QA-Banco", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31));
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 31));
        var clock = new TimeProviderStub(new DateTimeOffset(2026, 10, 31, 1, 37, 0, TimeSpan.Zero));
        var useCase = CriarCaso(position, asset, clock);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "A data de referência padrão (2026-10-30, a data de hoje em Brasília, pois não há cotação de CDI "
            + "importada) é anterior à data de aporte (2026-10-31). Informe referenceDate.",
            response.Message);
    }

    [Fact]
    public async Task Data_explicita_anterior_ao_aporte_mantem_a_mensagem_simples()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "QA-06", "QA-Banco", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31));
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 31));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 10, 30));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("A data de referência não pode ser anterior à data de aporte.", response.Message);
    }

    [Fact]
    public async Task Data_de_referencia_explicita_nao_e_alterada_pela_ultima_cotacao_de_cdi()
    {
        // Arrange: há CDI até 2025-04-17, mas a data informada (2026-01-01) prevalece.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert: caso 1.3 da spec.
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(365, response.Data!.ElapsedDays);
        Assert.Equal(1_100.00m, response.Data!.GrossAmount, precision: 2);
    }

    [Fact]
    public async Task Data_informada_e_devolvida_como_data_de_referencia_junto_com_a_data_do_aporte()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
        Assert.Equal(new DateOnly(2025, 4, 1), response.Data!.ApplicationDate);
    }

    [Fact]
    public async Task Data_ausente_com_cdi_importado_devolve_o_dia_util_seguinte_a_ultima_cotacao_como_data_de_referencia()
    {
        // Arrange: CDI até quinta, 2025-04-17; a data padrão é terça, 2025-04-22.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
        Assert.Equal(new DateOnly(2025, 4, 1), response.Data!.ApplicationDate);
    }

    [Fact]
    public async Task Data_ausente_sem_cdi_devolve_a_data_de_hoje_em_brasilia_como_data_de_referencia()
    {
        // Arrange: 2025-04-22 às 01h UTC ainda é 2025-04-21 em Brasília (UTC-3).
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var clock = new TimeProviderStub(new DateTimeOffset(2025, 4, 22, 1, 0, 0, TimeSpan.Zero));
        var useCase = CriarCaso(position, asset, clock);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 21), response.Data!.ReferenceDate);
        Assert.Equal(new DateOnly(2025, 4, 1), response.Data!.ApplicationDate);
    }

    // D-2: a data de referência posterior ao vencimento é devolvida como foi pedida, mas os valores
    // são os do vencimento (RN-02): 21 dias corridos e 13 úteis, os mesmos do caso de 2025-04-22.
    [Fact]
    public async Task Data_posterior_ao_vencimento_devolve_a_data_pedida_com_os_valores_do_vencimento()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 4, 22));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 5, 5));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 5, 5), response.Data!.ReferenceDate);
        Assert.Equal(new DateOnly(2025, 4, 1), response.Data!.ApplicationDate);
        Assert.Equal(21, response.Data!.ElapsedDays);
        Assert.Equal(13, response.Data!.BusinessDays);
        Assert.Equal(1_004.93m, response.Data!.GrossAmount);
        Assert.Equal(1.48m, response.Data!.IofAmount);
        Assert.Equal(0.78m, response.Data!.TaxAmount);
        Assert.Equal(1_002.67m, response.Data!.NetAmount);
    }

    // F12, RN-12: com a importação em dia, data informada depois da última cotação de CDI projeta com
    // o valor dela. Caso 1 da spec: CDB 100% do CDI de R$ 1.000, aplicação 2026-11-16, referência
    // 2026-11-26, cotação de 0,050788% em 16, 17, 18 e 19/11; 23, 24 e 25/11 são projetados (20/11 é
    // feriado). Bruto 1.003,56, IOF de 66% = 2,35, IR de 22,5% sobre 1,21 = 0,27, líquido 1.000,94.
    [Fact]
    public async Task Data_futura_de_cdi_com_importacao_em_dia_projeta_com_a_ultima_cotacao_e_marca_is_projected()
    {
        // Arrange
        var asset = CriarCdbDe2026(IndexType.Cdi, 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(position, asset, new DateOnly(2026, 11, 19), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.IsProjected);
        Assert.False(response.Data!.IsEstimated);
        Assert.Equal(10, response.Data!.ElapsedDays);
        Assert.Equal(7, response.Data!.BusinessDays);
        Assert.Equal(1_003.56m, response.Data!.GrossAmount);
        Assert.Equal(66m, response.Data!.IofRate);
        Assert.Equal(2.35m, response.Data!.IofAmount);
        Assert.Equal(0.27m, response.Data!.TaxAmount);
        Assert.Equal(1_000.94m, response.Data!.NetAmount);
    }

    // Caso 3a: a data padrão é o dia útil seguinte à última cotação (23/11), e o fim do intervalo é
    // exclusivo, então a projeção sem data nunca projeta CDI.
    [Fact]
    public async Task Sem_data_a_projecao_de_cdi_usa_so_cotacoes_publicadas_e_nao_marca_is_projected()
    {
        // Arrange
        var asset = CriarCdbDe2026(IndexType.Cdi, 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(position, asset, new DateOnly(2026, 11, 19), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2026, 11, 23), response.Data!.ReferenceDate);
        Assert.False(response.Data!.IsProjected);
        Assert.Equal(1_002.03m, response.Data!.GrossAmount);
        Assert.Equal(1_000.38m, response.Data!.NetAmount);
    }

    [Fact]
    public async Task Titulo_prefixado_com_data_futura_nao_marca_is_projected()
    {
        // Arrange: a taxa pré-fixada é contratada, então nunca é projetada, mesmo depois da última cotação.
        var asset = CriarCdbDe2026(IndexType.PreFixed, 10m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(position, asset, new DateOnly(2026, 11, 19), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.False(response.Data!.IsProjected);
        Assert.False(response.Data!.IsEstimated);
    }

    // Caso 6 da spec: IPCA+ 6%, R$ 1.000, aplicação 2026-10-01, referência 2026-12-15, hoje 2026-11-05.
    // Outubro (encerrado e não divulgado) usa a última variação, 0,82; novembro e dezembro usam a média
    // geométrica de 10/2025 a 09/2026, 0,37. As duas marcas ficam verdadeiras juntas.
    [Fact]
    public async Task Data_futura_de_ipca_marca_is_estimated_para_o_mes_encerrado_e_is_projected_para_o_mes_de_hoje()
    {
        // Arrange
        var asset = CriarCdbDe2026(IndexType.Ipca, 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 1));
        var useCase = CriarCasoDe2026(position, asset, new DateOnly(2026, 11, 5), IpcaDoCaso6());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 12, 15));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.IsEstimated);
        Assert.True(response.Data!.IsProjected);
        Assert.Equal(75, response.Data!.ElapsedDays);
        Assert.Equal(50, response.Data!.BusinessDays);
        Assert.Equal(1_025.42m, response.Data!.GrossAmount);
        Assert.Equal(5.72m, response.Data!.TaxAmount);
        Assert.Equal(1_019.70m, response.Data!.NetAmount);
    }

    // Mudança de comportamento 1: sem data, o IPCA+ com dia útil no mês de hoje deixa de ser estimado e
    // passa a projetado. Hoje 2026-11-05, última cotação de CDI 2026-11-04, então a data padrão é
    // 2026-11-05; aplicação em 2026-11-03: 2 dos 19 dias úteis de novembro (02/11 e 20/11 são feriados).
    // Bruto 1.000 x 1,0037^(2/19) x 1,06^(2/252) = 1.000,85.
    [Fact]
    public async Task Sem_data_o_ipca_mais_com_dia_util_no_mes_de_hoje_e_projetado_e_nao_estimado()
    {
        // Arrange
        var asset = CriarCdbDe2026(IndexType.Ipca, 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 3));
        var useCase = CriarCasoDe2026(
            position, asset, new DateOnly(2026, 11, 5),
            IpcaDoCaso6().Append(new IndexRate(IndexType.Cdi, new DateOnly(2026, 11, 4), 0.05m)).ToArray());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2026, 11, 5), response.Data!.ReferenceDate);
        Assert.True(response.Data!.IsProjected);
        Assert.False(response.Data!.IsEstimated);
        Assert.Equal(1_000.85m, response.Data!.GrossAmount);
    }

    // Referência no primeiro dia útil do mês de hoje (03/11, pois 02/11 é feriado): com o fim exclusivo,
    // nenhum dia de novembro entra no intervalo, então nada é projetado e a janela de 12 meses não é
    // exigida (só há 2 meses de IPCA). Outubro é estimado: 1.000 x 1,0082 x 1,06^(21/252) = 1.013,11.
    [Fact]
    public async Task Referencia_no_primeiro_dia_util_do_mes_de_hoje_nao_projeta_e_nao_exige_os_12_meses_de_ipca()
    {
        // Arrange
        var asset = CriarCdbDe2026(IndexType.Ipca, 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 1));
        var useCase = CriarCasoDe2026(
            position, asset, new DateOnly(2026, 11, 5),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 8, 1), -0.32m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 11, 3));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.False(response.Data!.IsProjected);
        Assert.True(response.Data!.IsEstimated);
        Assert.Equal(1_013.11m, response.Data!.GrossAmount);
    }

    [Fact]
    public async Task Sem_data_com_menos_de_12_meses_de_ipca_e_dia_util_no_mes_de_hoje_retorna_400_com_a_mensagem_da_janela()
    {
        // Arrange: 11 meses de IPCA (11/2025 a 09/2026); a janela vai de 10/2025 a 09/2026.
        var asset = CriarCdbDe2026(IndexType.Ipca, 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 3));
        var useCase = CriarCasoDe2026(
            position, asset, new DateOnly(2026, 11, 5),
            IpcaDoCaso6().Skip(1).Append(new IndexRate(IndexType.Cdi, new DateOnly(2026, 11, 4), 0.05m)).ToArray());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "A projeção futura do IPCA exige os 12 meses de 2025-10 a 2026-09; há 11, falta 2025-10.",
            response.Message);
    }

    [Fact]
    public async Task Data_futura_de_ipca_com_lacuna_na_janela_de_12_meses_retorna_400_com_o_primeiro_mes_ausente()
    {
        // Arrange: a janela 10/2025 a 09/2026 tem 11 meses, sem 02/2026.
        var asset = CriarCdbDe2026(IndexType.Ipca, 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 1));
        var useCase = CriarCasoDe2026(
            position, asset, new DateOnly(2026, 11, 5),
            IpcaDoCaso6().Where(rate => rate.ReferenceDate != new DateOnly(2026, 2, 1)).ToArray());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 12, 15));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "A projeção futura do IPCA exige os 12 meses de 2025-10 a 2026-09; há 11, falta 2026-02.",
            response.Message);
    }

    // Fronteira da mudança de comportamento 2, com o caso 3-bis: última cotação em 19/11 (quinta) e
    // 20/11 feriado. Hoje 24/11: a última cotação é de dois dias úteis antes (19/11 -> 23/11 -> 24/11),
    // o dia útil anterior a hoje é 23/11 e nenhum dia sem cotação fica antes dele.
    [Fact]
    public async Task Ultima_cotacao_de_cdi_dois_dias_uteis_antes_de_hoje_projeta_a_data_informada()
    {
        // Arrange
        var asset = CriarCdbDe2026(IndexType.Cdi, 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(position, asset, new DateOnly(2026, 11, 24), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 11, 26));

        // Assert: os mesmos valores do caso 1.
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.IsProjected);
        Assert.Equal(1_003.56m, response.Data!.GrossAmount);
        Assert.Equal(1_000.94m, response.Data!.NetAmount);
    }

    // Hoje 25/11: a última cotação (19/11) é de três dias úteis antes, o dia útil anterior a hoje é 24/11
    // e 23/11 ficou sem cotação antes dele: falha de importação, com o aviso do R-10.
    [Fact]
    public async Task Ultima_cotacao_de_cdi_tres_dias_uteis_antes_de_hoje_retorna_400_citando_o_primeiro_dia_sem_cotacao()
    {
        // Arrange
        var asset = CriarCdbDe2026(IndexType.Cdi, 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(position, asset, new DateOnly(2026, 11, 25), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "Cotação de CDI ausente para 2026-11-23. "
            + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.",
            response.Message);
    }

    private static FixedIncomeAsset CriarCdbDe2026(IndexType indexType, decimal rate) =>
        new("CDB Banco X", "Banco X", AssetType.Cdb, indexType, rate, new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));

    // Calendário com os feriados de 2026 e relógio fixo no dia dado: o mesmo TimeProvider no carregador
    // (fronteiras da RN-12) e no caso de uso (data padrão).
    private static GetPositionProjectionUseCase CriarCasoDe2026(
        Position position, FixedIncomeAsset asset, DateOnly today, params IndexRate[] rates)
    {
        var clock = new TimeProviderStub(new DateTimeOffset(today.Year, today.Month, today.Day, 15, 0, 0, TimeSpan.Zero));
        var loader = new MarketDataLoader(
            new HolidayRepositoryStub(TestMarket.HolidaysWith2026()), new IndexRateRepositoryStub(rates), clock);

        return new GetPositionProjectionUseCase(
            new PositionRepositoryStub(position), new FixedIncomeAssetRepositoryStub(asset), loader, clock);
    }

    // Série 12 do SGS cortada em 19/11/2026 (caso 1 da spec): 0,050788% ao dia em 16, 17, 18 e 19/11.
    private static IndexRate[] CdiDoCaso1() =>
        Enumerable.Range(16, 4)
            .Select(day => new IndexRate(IndexType.Cdi, new DateOnly(2026, 11, day), 0.050788m))
            .ToArray();

    // Série 433 de 10/2025 a 09/2026 (caso 6 da spec); último publicado: 09/2026.
    private static IndexRate[] IpcaDoCaso6()
    {
        decimal[] values = [0.09m, 0.18m, 0.33m, 0.33m, 0.70m, 0.88m, 0.67m, 0.58m, 0.16m, 0.07m, -0.32m, 0.82m];
        var first = new DateOnly(2025, 10, 1);

        return values
            .Select((value, index) => new IndexRate(IndexType.Ipca, first.AddMonths(index), value))
            .ToArray();
    }

    [Fact]
    public async Task Projecao_devolve_a_proxima_faixa_de_ir_e_o_fim_do_iof_do_caso_de_referencia()
    {
        // Arrange: caso 1a da spec da F13. Aporte em 2025-04-01, referência 2025-04-22 (d = 21):
        // próxima faixa 20,0 em aporte + 181 e fim do IOF em aporte + 30.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(20.0m, response.Data!.NextTaxRate);
        Assert.Equal(new DateOnly(2025, 9, 29), response.Data!.NextTaxRateDate);
        Assert.Equal(new DateOnly(2025, 5, 1), response.Data!.IofFreeDate);
    }

    private static GetPositionProjectionUseCase CriarCaso(
        Position? position, FixedIncomeAsset? asset, params IndexRate[] rates)
    {
        return CriarCaso(position, asset, new TimeProviderStub(Now), rates);
    }

    private static GetPositionProjectionUseCase CriarCaso(
        Position? position, FixedIncomeAsset? asset, TimeProvider clock, params IndexRate[] rates)
    {
        var loader = new MarketDataLoader(
            new HolidayRepositoryStub(TestMarket.Holidays()), new IndexRateRepositoryStub(rates), new TimeProviderStub(Now));

        return new GetPositionProjectionUseCase(
            new PositionRepositoryStub(position), new FixedIncomeAssetRepositoryStub(asset), loader, clock);
    }

    private sealed class TimeProviderStub : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public TimeProviderStub(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        // Brasília fixo em UTC-3 (sem horário de verão desde 2019), para os testes não
        // dependerem do fuso nem do tzdata da máquina.
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone(
            "America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }

    private sealed class HolidayRepositoryStub : IHolidayRepository
    {
        private readonly IEnumerable<Holiday> _holidays;

        public HolidayRepositoryStub(IEnumerable<Holiday> holidays)
        {
            _holidays = holidays;
        }

        public Task<IEnumerable<Holiday>> GetAllAsync() => Task.FromResult(_holidays);
    }

    private sealed class IndexRateRepositoryStub : IIndexRateRepository
    {
        private readonly IReadOnlyList<IndexRate> _rates;

        public IndexRateRepositoryStub(IReadOnlyList<IndexRate> rates)
        {
            _rates = rates;
        }

        public Task<IEnumerable<IndexRate>> GetByIndexTypeAndPeriodAsync(IndexType indexType, DateOnly from, DateOnly to) =>
            Task.FromResult(_rates
                .Where(r => r.IndexType == indexType && r.ReferenceDate >= from && r.ReferenceDate <= to)
                .AsEnumerable());

        public Task<IndexRate?> GetLastBeforeAsync(IndexType indexType, DateOnly date) =>
            Task.FromResult(_rates
                .Where(r => r.IndexType == indexType && r.ReferenceDate < date)
                .OrderByDescending(r => r.ReferenceDate)
                .FirstOrDefault());

        public Task<DateOnly?> GetLastReferenceDateAsync(IndexType indexType) =>
            Task.FromResult(_rates.Where(r => r.IndexType == indexType).Select(r => (DateOnly?)r.ReferenceDate).Max());

        public Task UpsertAsync(IEnumerable<IndexRate> rates) => Task.CompletedTask;
    }

    private sealed class FixedIncomeAssetRepositoryStub : IFixedIncomeAssetRepository
    {
        private readonly FixedIncomeAsset? _asset;

        public FixedIncomeAssetRepositoryStub(FixedIncomeAsset? asset)
        {
            _asset = asset;
        }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync() =>
            Task.FromResult(Enumerable.Empty<FixedIncomeAsset>());

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) => Task.FromResult(_asset);

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset) => Task.CompletedTask;
    }

    private sealed class PositionRepositoryStub : IPositionRepository
    {
        private readonly Position? _position;

        public PositionRepositoryStub(Position? position)
        {
            _position = position;
        }

        public Task<IEnumerable<Position>> GetAllAsync() => Task.FromResult(Enumerable.Empty<Position>());

        public Task<Position?> GetByIdAsync(Guid id) => Task.FromResult(_position);

        public Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId) =>
            Task.FromResult(Enumerable.Empty<Position>());

        public Task AddAsync(Position position) => Task.CompletedTask;
    }
}
