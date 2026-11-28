using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Application.Tests.TestData;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetPortfolioSummaryUseCaseTests
{
    // Relógio bem distante das datas dos testes: se o caso de uso usasse a data de hoje onde deve
    // usar a última cotação de CDI, os valores esperados não bateriam.
    private static readonly DateTimeOffset Now = new(2025, 12, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Caminho_de_sucesso_com_tipos_diferentes_retorna_200_com_totais_por_tipo()
    {
        // Arrange
        var cdb = CriarAsset(AssetType.Cdb);
        var lci = CriarAsset(AssetType.Lci);
        var positionCdb = new Position(cdb, 1_000m, cdb.IssueDate);
        var positionLci = new Position(lci, 500m, lci.IssueDate);
        var useCase = CriarCaso(new[] { positionCdb, positionLci }, new[] { cdb, lci });

        // Act
        var response = await useCase.ExecuteAsync(cdb.IssueDate.AddDays(365));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, response.Data!.PositionCount);
        Assert.Equal(2, response.Data!.ByAssetType.Count);
    }

    [Fact]
    public async Task Resumo_devolve_net_yield_nos_itens_e_total_net_yield_igual_ao_liquido_menos_o_investido()
    {
        // Arrange: caso 16 da spec da F13. LCI de R$ 1.500 (líquido 1.504,57; net yield 4,57) e dois
        // aportes de R$ 1.000 no CDB (líquido 1.002,67 cada; net yield 2,67), referência 2025-04-22.
        var cdb = CriarAsset(AssetType.Cdb, name: "CDB A");
        var lci = CriarAsset(AssetType.Lci, name: "LCI B");
        var lciPosition = new Position(lci, 1_500m, new DateOnly(2025, 4, 2));
        var firstCdb = new Position(cdb, 1_000m, new DateOnly(2025, 4, 1));
        var secondCdb = new Position(cdb, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { lciPosition, firstCdb, secondCdb }, new[] { cdb, lci });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        Assert.Equal(9.91m, data.TotalNetYield);
        Assert.Equal(data.TotalNetAmount - data.TotalInvestedAmount, data.TotalNetYield);
        Assert.Equal(data.Positions.Sum(p => p.Projection.NetYield), data.TotalNetYield);
        Assert.Equal(4.57m, Assert.Single(data.Positions, p => p.Projection.PositionId == lciPosition.Id).Projection.NetYield);
        Assert.Equal(2.67m, Assert.Single(data.Positions, p => p.Projection.PositionId == firstCdb.Id).Projection.NetYield);
    }

    [Fact]
    public async Task Carteira_vazia_retorna_200_com_resumo_zerado()
    {
        // Arrange
        var useCase = CriarCaso(Array.Empty<Position>(), Array.Empty<FixedIncomeAsset>());

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(0, response.Data!.PositionCount);
        Assert.Equal(0m, response.Data!.TotalNetAmount);
        Assert.Equal(0m, response.Data!.TotalIofAmount);
        Assert.Empty(response.Data!.ByAssetType);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_usa_o_dia_util_seguinte_a_ultima_cotacao_de_cdi()
    {
        // Arrange: a última cotação de CDI é quinta, 2025-04-17; 18/04 (Paixão) e 21/04 (Tiradentes)
        // são feriados, então o dia útil seguinte é terça, 2025-04-22 (e não a data de hoje).
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            new[] { position }, new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert: PreFixed 10% a.a., 2025-04-01 a 2025-04-22. Derivação do esperado:
        // - 13 dias úteis (18/04 e 21/04 são feriados), fator 1,10^(13/252) = 1,0049289,
        //   bruto 1.000 x 1,0049289 = 1.004,93 e rendimento 4,93;
        // - 21 dias corridos: IOF de 30% sobre o rendimento = 1,479, arredondado para 1,48;
        // - IR de 22,5% sobre o rendimento menos o IOF: 22,5% de (4,93 - 1,48 = 3,45) = 0,77625,
        //   arredondado para 0,78; líquido 1.004,93 - 1,48 - 0,78 = 1.002,67.
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1_004.93m, response.Data!.TotalGrossAmount);
        Assert.Equal(1.48m, response.Data!.TotalIofAmount);
        Assert.Equal(0.78m, response.Data!.TotalTaxAmount);
        Assert.Equal(1_002.67m, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_e_sem_nenhuma_cotacao_de_cdi_usa_a_data_de_hoje_do_relogio()
    {
        // Arrange: sem CDI importado não há data de mercado; vale a data de hoje do TimeProvider,
        // fixada em 2025-04-22 (mesma data do teste anterior, para os mesmos valores).
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var clock = new TimeProviderStub(new DateTimeOffset(2025, 4, 22, 12, 0, 0, TimeSpan.Zero));
        var useCase = CriarCaso(new[] { position }, new[] { asset }, clock);

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1_004.93m, response.Data!.TotalGrossAmount);
        Assert.Equal(1_002.67m, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Aporte_na_data_padrao_sem_data_informada_rende_zero_sem_iof_nem_ir()
    {
        // Arrange: última cotação de CDI em quinta, 2025-04-17, então a data padrão é 2025-04-22.
        // O título é em CDI e só há cotação até 17/04: o aporte de 22/04 não tem dia útil a projetar.
        var asset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 22));
        var useCase = CriarCaso(
            new[] { position }, new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1_000m, response.Data!.TotalGrossAmount);
        Assert.Equal(0m, response.Data!.TotalGrossYield);
        Assert.Equal(0m, response.Data!.TotalIofAmount);
        Assert.Equal(0m, response.Data!.TotalTaxAmount);
        Assert.Equal(1_000m, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Aporte_posterior_a_data_padrao_sem_data_informada_rende_zero_e_retorna_200()
    {
        // Arrange: data padrão 2025-04-22; o aporte é de segunda, 2025-05-05, sem cotação de CDI.
        var asset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(
            new[] { position }, new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, response.Data!.PositionCount);
        Assert.Equal(1_000m, response.Data!.TotalGrossAmount);
        Assert.Equal(0m, response.Data!.TotalGrossYield);
        Assert.Equal(0m, response.Data!.TotalIofAmount);
        Assert.Equal(0m, response.Data!.TotalTaxAmount);
        Assert.Equal(1_000m, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Carteira_mista_sem_data_informada_soma_posicao_antiga_com_rendimento_e_recente_sem_rendimento_e_fecha_a_rn_07()
    {
        // Arrange: data padrão 2025-04-22. A posição antiga (PreFixed 10%, 1.000 desde 2025-04-01)
        // rende como no caso de 21 dias corridos: bruto 1.004,93, IOF 1,48, IR 0,78, líquido 1.002,67.
        // A recente (500, aporte em 2025-05-05, depois da data padrão) rende zero.
        var oldAsset = CriarAsset(AssetType.Cdb);
        var recentAsset = CriarAsset(AssetType.Lci, IndexType.Cdi, rate: 100m);
        var oldPosition = new Position(oldAsset, 1_000m, new DateOnly(2025, 4, 1));
        var recentPosition = new Position(recentAsset, 500m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(
            new[] { oldPosition, recentPosition },
            new[] { oldAsset, recentAsset },
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1_500m, response.Data!.TotalInvestedAmount);
        Assert.Equal(1_504.93m, response.Data!.TotalGrossAmount);
        Assert.Equal(4.93m, response.Data!.TotalGrossYield);
        Assert.Equal(1.48m, response.Data!.TotalIofAmount);
        Assert.Equal(0.78m, response.Data!.TotalTaxAmount);
        Assert.Equal(1_502.67m, response.Data!.TotalNetAmount);
        Assert.Equal(
            response.Data!.TotalGrossAmount - response.Data!.TotalIofAmount - response.Data!.TotalTaxAmount,
            response.Data!.TotalNetAmount);
    }

    // R-02: com data explícita, posição aportada depois dela fica fora do resumo em vez de derrubá-lo.
    // Caso de referência: PreFixed 10%, R$ 1.000 desde 2025-04-01 até 2025-04-22 (13 dias úteis,
    // 21 corridos): bruto 1.004,93, rendimento 4,93, IOF 1,48, IR 0,78, líquido 1.002,67.
    [Fact]
    public async Task Data_explicita_deixa_de_fora_a_posicao_aportada_depois_dela_e_fecha_a_rn_07()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var before = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var after = new Position(asset, 2_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { before, after }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, response.Data!.PositionCount);
        Assert.Equal(1_000m, response.Data!.TotalInvestedAmount);
        Assert.Equal(1_004.93m, response.Data!.TotalGrossAmount);
        Assert.Equal(4.93m, response.Data!.TotalGrossYield);
        Assert.Equal(1.48m, response.Data!.TotalIofAmount);
        Assert.Equal(0.78m, response.Data!.TotalTaxAmount);
        Assert.Equal(1_002.67m, response.Data!.TotalNetAmount);
        Assert.Equal(
            response.Data!.TotalGrossAmount - response.Data!.TotalTaxAmount - response.Data!.TotalIofAmount,
            response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Data_explicita_anterior_a_todos_os_aportes_retorna_200_com_totais_zerados()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { position }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(0, response.Data!.PositionCount);
        Assert.Equal(0m, response.Data!.TotalInvestedAmount);
        Assert.Equal(0m, response.Data!.TotalGrossAmount);
        Assert.Equal(0m, response.Data!.TotalGrossYield);
        Assert.Equal(0m, response.Data!.TotalIofAmount);
        Assert.Equal(0m, response.Data!.TotalTaxAmount);
        Assert.Equal(0m, response.Data!.TotalNetAmount);
        Assert.Empty(response.Data!.ByAssetType);
    }

    [Fact]
    public async Task Posicoes_fora_do_resumo_nao_entram_no_carregamento_do_mercado()
    {
        // Arrange: a data informada (2024-01-01) é anterior ao período coberto pelo calendário de
        // teste (a partir de 2025-01-01). Se o mercado fosse carregado para a posição excluída,
        // o calendário recusaria a data e o resumo viraria 400.
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { position }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2024, 1, 1));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(0, response.Data!.PositionCount);
    }

    [Fact]
    public async Task Posicao_aportada_exatamente_na_data_de_referencia_entra_no_resumo_com_rendimento_zero()
    {
        // Arrange: a antiga rende como no caso de 21 dias; a nova (500) entra com rendimento zero.
        var asset = CriarAsset(AssetType.Cdb);
        var old = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var sameDay = new Position(asset, 500m, new DateOnly(2025, 4, 22));
        var useCase = CriarCaso(new[] { old, sameDay }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, response.Data!.PositionCount);
        Assert.Equal(1_500m, response.Data!.TotalInvestedAmount);
        Assert.Equal(1_504.93m, response.Data!.TotalGrossAmount);
        Assert.Equal(4.93m, response.Data!.TotalGrossYield);
        Assert.Equal(1_502.67m, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Verificacao_de_titulo_inexistente_vem_antes_da_exclusao_por_data()
    {
        // Arrange: a posição seria excluída pela data (aporte depois dela), mas o título dela não existe.
        var asset = CriarAsset(AssetType.Cdb);
        var orphan = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { orphan }, Array.Empty<FixedIncomeAsset>());

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    // R-01: caso do QA no resumo. CDB pré-fixado de 12%, R$ 1.000 em 2026-10-28, sem cotação de
    // CDI, às 22h37 de 30/10 em Brasília: a data de hoje é 2026-10-30 (2 dias corridos).
    [Fact]
    public async Task Sem_data_e_sem_cotacao_de_cdi_entre_21h_e_meia_noite_em_brasilia_usa_a_data_de_brasilia()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb, rate: 12m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 28));
        var clock = new TimeProviderStub(new DateTimeOffset(2026, 10, 31, 1, 37, 0, TimeSpan.Zero));
        var useCase = CriarCaso(new[] { position }, new[] { asset }, clock);

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1_000.90m, response.Data!.TotalGrossAmount);
        Assert.Equal(0.84m, response.Data!.TotalIofAmount);
        Assert.Equal(0.01m, response.Data!.TotalTaxAmount);
        Assert.Equal(1_000.05m, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Data_de_hoje_no_resumo_muda_para_o_dia_seguinte_so_a_partir_de_03h_utc()
    {
        // Arrange: às 03h00 UTC é 00h00 de 31/10 em Brasília: 3 dias corridos, IOF de 90% (QA: bruto 1.001,35).
        var asset = CriarAsset(AssetType.Cdb, rate: 12m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 28));
        var clock = new TimeProviderStub(new DateTimeOffset(2026, 10, 31, 3, 0, 0, TimeSpan.Zero));
        var useCase = CriarCaso(new[] { position }, new[] { asset }, clock);

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1_001.35m, response.Data!.TotalGrossAmount);
        Assert.Equal(1.22m, response.Data!.TotalIofAmount);
    }

    [Fact]
    public async Task Data_explicita_sem_cotacao_de_cdi_retorna_400_com_a_primeira_data_faltante()
    {
        // Arrange: terça 2025-04-01 a sexta 2025-04-04; só há CDI de 01/04, faltam 02/04 e 03/04.
        var asset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            new[] { position }, new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 1), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 4));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("2025-04-02", response.Message);
    }

    [Fact]
    public async Task Cotacao_de_cdi_ausente_identifica_a_posicao_e_lembra_da_importacao()
    {
        // Arrange: mesma situação do teste anterior; só há CDI de 01/04, falta 02/04.
        var asset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            new[] { position }, new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 1), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 4));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            $"Posição {position.Id} (título 'Título Teste'): Cotação de CDI ausente para 2025-04-02. "
            + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.",
            response.Message);
    }

    [Fact]
    public async Task Erro_de_dominio_sem_relacao_com_cotacao_mantem_a_mensagem_original()
    {
        // Arrange: data explícita fora da cobertura do calendário não é falta de cotação.
        var asset = new FixedIncomeAsset(
            "Título Teste", "Emissor Teste", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2035, 1, 1));
        var position = new Position(asset, 1_000m, asset.IssueDate);
        var useCase = CriarCaso(new[] { position }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2031, 6, 1));

        // Assert
        Assert.DoesNotContain("importação", response.Message);
    }

    [Fact]
    public async Task Data_de_referencia_fora_da_cobertura_do_calendario_retorna_400()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "Título Teste", "Emissor Teste", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2035, 1, 1));
        var position = new Position(asset, 1_000m, asset.IssueDate);
        var useCase = CriarCaso(new[] { position }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2031, 6, 1));

        // Assert
        Assert.Equal(400, response.StatusCode);
        // B-05: cita a data enviada (e não a véspera) e o período coberto inteiro.
        Assert.Equal(
            "Data 2031-06-01 fora do período coberto pelo calendário (2025-01-01 a 2030-12-31).",
            response.Message);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404_mesmo_quando_o_calendario_nao_carrega()
    {
        // Arrange: sem feriados cadastrados o carregamento de mercado lança DomainException (400).
        // O 404 precisa vencer: a existência dos títulos é verificada antes de carregar o mercado.
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, asset.IssueDate);
        var useCase = new GetPortfolioSummaryUseCase(
            new PositionRepositoryStub(new[] { position }),
            new FixedIncomeAssetRepositoryStub(Array.Empty<FixedIncomeAsset>()),
            new PositionProjector(
                new MarketDataLoader(
                    new HolidayRepositoryStub(Array.Empty<Holiday>()),
                    new IndexRateRepositoryStub(Array.Empty<IndexRate>()),
                    new TimeProviderStub(Now)),
                new TimeProviderStub(Now)));

        // Act
        var response = await useCase.ExecuteAsync(asset.IssueDate.AddDays(30));

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains(position.Id.ToString(), response.Message);
    }

    [Fact]
    public async Task Titulos_sao_carregados_uma_unica_vez_independente_do_numero_de_posicoes()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var positions = Enumerable.Range(0, 5)
            .Select(_ => new Position(asset, 1_000m, asset.IssueDate))
            .ToArray();
        var assetRepository = new FixedIncomeAssetRepositoryStub(new[] { asset });
        var useCase = new GetPortfolioSummaryUseCase(
            new PositionRepositoryStub(positions),
            assetRepository,
            new PositionProjector(CriarLoader(), new TimeProviderStub(Now)));

        // Act
        var response = await useCase.ExecuteAsync(asset.IssueDate.AddDays(30));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, assetRepository.GetAllCallCount);
    }

    [Fact]
    public async Task Data_informada_e_a_data_de_referencia_da_raiz_do_resumo()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { position }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
    }

    [Fact]
    public async Task Data_omitida_com_cdi_importado_devolve_a_data_padrao_como_data_de_referencia_da_raiz()
    {
        // Arrange: CDI até quinta, 2025-04-17; 18/04 e 21/04 são feriados, a data padrão é terça, 2025-04-22.
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            new[] { position }, new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
    }

    [Fact]
    public async Task Carteira_vazia_sem_data_devolve_a_data_padrao_como_data_de_referencia()
    {
        // Arrange: mesma última cotação de CDI do teste anterior, sem nenhuma posição.
        var useCase = CriarCaso(
            Array.Empty<Position>(), Array.Empty<FixedIncomeAsset>(),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(0, response.Data!.PositionCount);
        Assert.Equal(0m, response.Data!.TotalNetAmount);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
        Assert.Empty(response.Data!.Positions);
    }

    [Fact]
    public async Task Carteira_vazia_sem_data_e_sem_cdi_devolve_hoje_em_brasilia_como_data_de_referencia()
    {
        // Arrange: às 01h30 UTC de 23/04 ainda é 22/04 em Brasília (UTC-3).
        var clock = new TimeProviderStub(new DateTimeOffset(2025, 4, 23, 1, 30, 0, TimeSpan.Zero));
        var useCase = CriarCaso(Array.Empty<Position>(), Array.Empty<FixedIncomeAsset>(), clock);

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
    }

    [Fact]
    public async Task Data_informada_anterior_a_todos_os_aportes_devolve_a_informada_como_data_de_referencia()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { position }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(0, response.Data!.PositionCount);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
        Assert.Empty(response.Data!.Positions);
    }

    [Fact]
    public async Task Carteira_mista_sem_data_mantem_a_data_padrao_na_raiz_mesmo_com_aporte_posterior()
    {
        // Arrange: cenário da carteira mista; a raiz é a data padrão (2025-04-22), não a do aporte de 05/05.
        // O valor do aporte posterior (500, sem rendimento) prova que a data informada (nula) não foi
        // sobrescrita pela data padrão já resolvida; senão o aporte posterior viraria 400.
        var oldAsset = CriarAsset(AssetType.Cdb);
        var recentAsset = CriarAsset(AssetType.Lci, IndexType.Cdi, rate: 100m);
        var oldPosition = new Position(oldAsset, 1_000m, new DateOnly(2025, 4, 1));
        var recentPosition = new Position(recentAsset, 500m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(
            new[] { oldPosition, recentPosition },
            new[] { oldAsset, recentAsset },
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
        Assert.Equal(1_502.67m, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Titulo_inexistente_sem_data_retorna_404_mesmo_com_cdi_e_calendario_vazio()
    {
        // Arrange: com CDI no stub e sem feriados, resolver a data padrão lançaria DomainException (400).
        // O 404 precisa vencer: a resolução da data não pode subir para antes da verificação de existência.
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, asset.IssueDate);
        var useCase = CriarCasoComCalendarioVazio(
            new[] { position }, Array.Empty<FixedIncomeAsset>(),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains(position.Id.ToString(), response.Message);
    }

    [Fact]
    public async Task Carteira_vazia_sem_data_com_cdi_e_calendario_vazio_retorna_400_com_a_mensagem_do_calendario()
    {
        // Arrange: consequência aceita da D-4. A resolução da data padrão com a carteira vazia
        // acontece dentro do tratamento de DomainException.
        var useCase = CriarCasoComCalendarioVazio(
            Array.Empty<Position>(), Array.Empty<FixedIncomeAsset>(),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("O calendário precisa de ao menos um feriado para definir o período coberto.", response.Message);
    }

    [Fact]
    public async Task Cada_item_traz_a_projecao_da_posicao_e_o_titulo_dela_com_duas_posicoes_de_titulos_diferentes()
    {
        // Arrange: caso de referência (PreFixed 10%, R$ 1.000 de 2025-04-01 a 2025-04-22). CDB: bruto
        // 1.004,93, IOF 1,48, IR 0,78, líquido 1.002,67. LCI: isenta de IR, IOF 1,48, líquido 1.003,45.
        var cdb = CriarAsset(AssetType.Cdb, name: "CDB Banco X", issuer: "Banco X");
        var lci = CriarAsset(AssetType.Lci, name: "LCI Banco Y", issuer: "Banco Y");
        var cdbPosition = new Position(cdb, 1_000m, new DateOnly(2025, 4, 1));
        var lciPosition = new Position(lci, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { cdbPosition, lciPosition }, new[] { cdb, lci });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, response.Data!.Positions.Count);

        var cdbItem = response.Data!.Positions.Single(p => p.Projection.PositionId == cdbPosition.Id);
        Assert.Equal(cdb.Id, cdbItem.Projection.AssetId);
        Assert.Equal(new DateOnly(2025, 4, 1), cdbItem.Projection.ApplicationDate);
        Assert.Equal(new DateOnly(2025, 4, 22), cdbItem.Projection.ReferenceDate);
        Assert.Equal(1_000m, cdbItem.Projection.InvestedAmount);
        Assert.Equal(1_004.93m, cdbItem.Projection.GrossAmount);
        Assert.Equal(1.48m, cdbItem.Projection.IofAmount);
        Assert.Equal(0.78m, cdbItem.Projection.TaxAmount);
        Assert.Equal(1_002.67m, cdbItem.Projection.NetAmount);
        Assert.Equal(cdb.Id, cdbItem.Asset.Id);
        Assert.Equal("CDB Banco X", cdbItem.Asset.Name);
        Assert.Equal("Banco X", cdbItem.Asset.Issuer);
        Assert.Equal("Cdb", cdbItem.Asset.AssetType);
        Assert.Equal("PreFixed", cdbItem.Asset.IndexType);
        Assert.Equal(10m, cdbItem.Asset.Rate);

        var lciItem = response.Data!.Positions.Single(p => p.Projection.PositionId == lciPosition.Id);
        Assert.Equal(lci.Id, lciItem.Projection.AssetId);
        Assert.Equal(new DateOnly(2025, 4, 1), lciItem.Projection.ApplicationDate);
        Assert.Equal(1_004.93m, lciItem.Projection.GrossAmount);
        Assert.Equal(1.48m, lciItem.Projection.IofAmount);
        Assert.Equal(0m, lciItem.Projection.TaxAmount);
        Assert.Equal(1_003.45m, lciItem.Projection.NetAmount);
        Assert.Equal(lci.Id, lciItem.Asset.Id);
        Assert.Equal("LCI Banco Y", lciItem.Asset.Name);
        Assert.Equal("Banco Y", lciItem.Asset.Issuer);
        Assert.Equal("Lci", lciItem.Asset.AssetType);
        Assert.Equal("PreFixed", lciItem.Asset.IndexType);
        Assert.Equal(10m, lciItem.Asset.Rate);
    }

    [Fact]
    public async Task Duas_posicoes_do_mesmo_titulo_geram_dois_itens_com_o_mesmo_titulo_e_posicoes_diferentes()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var first = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var second = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { first, second }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, response.Data!.Positions.Count);
        Assert.All(response.Data!.Positions, item => Assert.Equal(asset.Id, item.Asset.Id));
        Assert.All(response.Data!.Positions, item => Assert.Equal(asset.Id, item.Projection.AssetId));
        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(id => id),
            response.Data!.Positions.Select(p => p.Projection.PositionId).OrderBy(id => id));
    }

    // Caso 12a: duas posições do mesmo CDB. O IR total de 1,56 (soma dos itens, 2 x 0,78) e o líquido
    // de 2.005,34 distinguem a RN-07 de um consolidado recalculado: o IR de R$ 2.000 daria 1,55 e o
    // líquido 2.005,35.
    [Fact]
    public async Task Soma_dos_itens_do_mesmo_cdb_fecha_com_os_totais_e_nao_com_um_consolidado_recalculado()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var first = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var second = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { first, second }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        Assert.All(data.Positions, item =>
        {
            Assert.Equal(1_004.93m, item.Projection.GrossAmount);
            Assert.Equal(1.48m, item.Projection.IofAmount);
            Assert.Equal(0.78m, item.Projection.TaxAmount);
            Assert.Equal(1_002.67m, item.Projection.NetAmount);
        });
        Assert.Equal(2_000m, data.TotalInvestedAmount);
        Assert.Equal(2_009.86m, data.TotalGrossAmount);
        Assert.Equal(9.86m, data.TotalGrossYield);
        Assert.Equal(2.96m, data.TotalIofAmount);
        Assert.Equal(1.56m, data.TotalTaxAmount);
        Assert.Equal(2_005.34m, data.TotalNetAmount);
        AssertFechamento(data);
    }

    // Caso 12b: CDB, LCI e LCA, todos PreFixed 10%, R$ 1.000 de 2025-04-01 a 2025-04-22.
    [Fact]
    public async Task Soma_dos_itens_de_cdb_lci_e_lca_fecha_com_os_totais()
    {
        // Arrange
        var cdb = CriarAsset(AssetType.Cdb);
        var lci = CriarAsset(AssetType.Lci);
        var lca = CriarAsset(AssetType.Lca);
        var cdbPosition = new Position(cdb, 1_000m, new DateOnly(2025, 4, 1));
        var lciPosition = new Position(lci, 1_000m, new DateOnly(2025, 4, 1));
        var lcaPosition = new Position(lca, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { cdbPosition, lciPosition, lcaPosition }, new[] { cdb, lci, lca });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        Assert.Equal(1_002.67m, data.Positions.Single(p => p.Projection.PositionId == cdbPosition.Id).Projection.NetAmount);

        var lciProjection = data.Positions.Single(p => p.Projection.PositionId == lciPosition.Id).Projection;
        Assert.Equal(0m, lciProjection.TaxAmount);
        Assert.Equal(1.48m, lciProjection.IofAmount);
        Assert.Equal(1_003.45m, lciProjection.NetAmount);

        var lcaProjection = data.Positions.Single(p => p.Projection.PositionId == lcaPosition.Id).Projection;
        Assert.Equal(0m, lcaProjection.IofAmount);
        Assert.Equal(0m, lcaProjection.TaxAmount);
        Assert.Equal(1_004.93m, lcaProjection.NetAmount);

        Assert.Equal(3_000m, data.TotalInvestedAmount);
        Assert.Equal(3_014.79m, data.TotalGrossAmount);
        Assert.Equal(14.79m, data.TotalGrossYield);
        Assert.Equal(2.96m, data.TotalIofAmount);
        Assert.Equal(0.78m, data.TotalTaxAmount);
        Assert.Equal(3_011.05m, data.TotalNetAmount);
        AssertFechamento(data);
    }

    [Fact]
    public async Task Sem_data_o_item_da_posicao_com_aporte_posterior_a_data_padrao_usa_a_data_do_aporte()
    {
        // Arrange: cenário da carteira mista. Data padrão 2025-04-22 (CDI até 2025-04-17); a LCI de
        // R$ 500 foi aportada em 2025-05-05 e rende zero, projetada na própria data do aporte (D-3).
        var oldAsset = CriarAsset(AssetType.Cdb);
        var recentAsset = CriarAsset(AssetType.Lci, IndexType.Cdi, rate: 100m);
        var oldPosition = new Position(oldAsset, 1_000m, new DateOnly(2025, 4, 1));
        var recentPosition = new Position(recentAsset, 500m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(
            new[] { oldPosition, recentPosition },
            new[] { oldAsset, recentAsset },
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        Assert.Equal(new DateOnly(2025, 4, 22), data.ReferenceDate);
        Assert.Equal(2, data.Positions.Count);

        var recent = data.Positions.Single(p => p.Projection.PositionId == recentPosition.Id).Projection;
        Assert.Equal(new DateOnly(2025, 5, 5), recent.ReferenceDate);
        Assert.Equal(new DateOnly(2025, 5, 5), recent.ApplicationDate);
        Assert.Equal(500m, recent.InvestedAmount);
        Assert.Equal(500m, recent.GrossAmount);
        Assert.Equal(500m, recent.NetAmount);
        Assert.Equal(0m, recent.GrossYield);
        Assert.Equal(0m, recent.IofAmount);
        Assert.Equal(0m, recent.TaxAmount);
        Assert.Equal(0, recent.ElapsedDays);
        Assert.Equal(0, recent.BusinessDays);

        var old = data.Positions.Single(p => p.Projection.PositionId == oldPosition.Id).Projection;
        Assert.Equal(new DateOnly(2025, 4, 22), old.ReferenceDate);
        Assert.Equal(new DateOnly(2025, 4, 1), old.ApplicationDate);
        Assert.Equal(1_002.67m, old.NetAmount);
        AssertFechamento(data);
    }

    [Fact]
    public async Task Data_informada_deixa_a_posicao_aportada_depois_dela_fora_de_positions_e_o_fechamento_vale_com_as_que_ficaram()
    {
        // Arrange: duas posições dentro e uma aportada depois da data (R-02).
        var asset = CriarAsset(AssetType.Cdb);
        var first = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var second = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var later = new Position(asset, 2_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { first, second, later }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        Assert.Equal(2, data.PositionCount);
        Assert.DoesNotContain(data.Positions, item => item.Projection.PositionId == later.Id);
        Assert.Equal(2_005.34m, data.TotalNetAmount);
        AssertFechamento(data);
    }

    [Fact]
    public async Task Itens_saem_na_ordem_em_que_o_repositorio_devolveu_as_posicoes()
    {
        // Arrange: aporte mais recente primeiro, como o repositório devolve.
        var asset = CriarAsset(AssetType.Cdb);
        var newest = new Position(asset, 1_000m, new DateOnly(2025, 4, 10));
        var middle = new Position(asset, 1_000m, new DateOnly(2025, 4, 5));
        var oldest = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { newest, middle, oldest }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(
            new[] { newest.Id, middle.Id, oldest.Id },
            response.Data!.Positions.Select(p => p.Projection.PositionId));
    }

    [Fact]
    public async Task Item_reflete_o_is_estimated_da_projecao_com_o_caso_ipca_de_mes_sem_divulgacao()
    {
        // Arrange: cenário de PositionIpcaProjectionTests (caso 6.3). IPCA+ 6%, R$ 1.000 de 2026-09-01
        // a 2026-10-15, IPCA publicado até setembro (0,82): outubro usa o último publicado, então a
        // projeção é estimada. Bruto 1.018,78, IR 4,23, líquido 1.014,55. A posição em CDB PreFixed
        // da mesma data não é estimada.
        var ipcaAsset = new FixedIncomeAsset(
            "Título IPCA+", "Emissor Teste", AssetType.Cdb, IndexType.Ipca, 6m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var preFixedAsset = new FixedIncomeAsset(
            "CDB Pré", "Emissor Teste", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var ipcaPosition = new Position(ipcaAsset, 1_000m, new DateOnly(2026, 9, 1));
        var preFixedPosition = new Position(preFixedAsset, 1_000m, new DateOnly(2026, 9, 1));
        var useCase = CriarCasoComFeriadosDe2026(
            new[] { ipcaPosition, preFixedPosition },
            new[] { ipcaAsset, preFixedAsset },
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 8, 1), -0.32m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m));

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2026, 10, 15));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var ipcaItem = response.Data!.Positions.Single(p => p.Projection.PositionId == ipcaPosition.Id).Projection;
        Assert.True(ipcaItem.IsEstimated);
        Assert.Equal(1_018.78m, ipcaItem.GrossAmount);
        Assert.Equal(4.23m, ipcaItem.TaxAmount);
        Assert.Equal(1_014.55m, ipcaItem.NetAmount);

        var preFixedItem = response.Data!.Positions.Single(p => p.Projection.PositionId == preFixedPosition.Id).Projection;
        Assert.False(preFixedItem.IsEstimated);
        AssertFechamento(response.Data!);
    }

    // F12, RN-12 e D-6: a raiz é verdadeira se algum item for projetado. Caso 1 da spec: CDB 100% do CDI
    // de R$ 1.000 (aplicação 2026-11-16, referência 2026-11-26, hoje 2026-11-19, cotação de 0,050788%
    // em 16 a 19/11), bruto 1.003,56 e líquido 1.000,94. O CDB pré-fixado do mesmo dia não é projetado.
    [Fact]
    public async Task Data_futura_com_cdi_projetado_marca_o_item_e_a_raiz_e_deixa_o_prefixado_sem_marca()
    {
        // Arrange
        var cdiAsset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m, name: "CDB CDI");
        var preFixedAsset = CriarAsset(AssetType.Cdb, IndexType.PreFixed, rate: 10m, name: "CDB Pré");
        var cdiPosition = new Position(cdiAsset, 1_000m, new DateOnly(2026, 11, 16));
        var preFixedPosition = new Position(preFixedAsset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(
            new[] { cdiPosition, preFixedPosition }, new[] { cdiAsset, preFixedAsset },
            new DateOnly(2026, 11, 19), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.IsProjected);

        var cdiItem = response.Data!.Positions.Single(p => p.Projection.PositionId == cdiPosition.Id).Projection;
        Assert.True(cdiItem.IsProjected);
        Assert.Equal(1_003.56m, cdiItem.GrossAmount);
        Assert.Equal(1_000.94m, cdiItem.NetAmount);

        var preFixedItem = response.Data!.Positions.Single(p => p.Projection.PositionId == preFixedPosition.Id).Projection;
        Assert.False(preFixedItem.IsProjected);
        AssertFechamento(response.Data!);
    }

    // Caso 6 da spec (IPCA+ 6%, aplicação 2026-10-01, referência 2026-12-15, hoje 2026-11-05): o item
    // sai com as duas marcas e a raiz com isProjected; isEstimated continua só no item.
    [Fact]
    public async Task Data_futura_com_ipca_marca_o_item_como_estimado_e_projetado_e_a_raiz_como_projetada()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb, IndexType.Ipca, rate: 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 1));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 5), IpcaDoCaso6());

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2026, 12, 15));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.IsProjected);
        var item = Assert.Single(response.Data!.Positions).Projection;
        Assert.True(item.IsEstimated);
        Assert.True(item.IsProjected);
        Assert.Equal(1_025.42m, item.GrossAmount);
        Assert.Equal(5.72m, item.TaxAmount);
        Assert.Equal(1_019.70m, item.NetAmount);
        Assert.Equal(1_025.42m, response.Data!.TotalGrossAmount);
    }

    [Fact]
    public async Task Sem_data_a_raiz_do_resumo_com_cdi_so_publicado_nao_marca_is_projected()
    {
        // Arrange: a data padrão (23/11) é o dia útil seguinte à última cotação, com fim exclusivo.
        var asset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 19), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.False(response.Data!.IsProjected);
        Assert.False(Assert.Single(response.Data!.Positions).Projection.IsProjected);
    }

    // Mudança de comportamento 1 no resumo: hoje 2026-11-05, data padrão 2026-11-05, IPCA+ aplicado em
    // 2026-11-03 com 2 dias úteis de novembro: projetado e não estimado, bruto 1.000,85.
    [Fact]
    public async Task Sem_data_o_ipca_mais_com_dia_util_no_mes_de_hoje_marca_o_item_e_a_raiz_como_projetados()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb, IndexType.Ipca, rate: 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 3));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 5),
            IpcaDoCaso6().Append(new IndexRate(IndexType.Cdi, new DateOnly(2026, 11, 4), 0.05m)).ToArray());

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2026, 11, 5), response.Data!.ReferenceDate);
        Assert.True(response.Data!.IsProjected);
        var item = Assert.Single(response.Data!.Positions).Projection;
        Assert.True(item.IsProjected);
        Assert.False(item.IsEstimated);
        Assert.Equal(1_000.85m, item.GrossAmount);
    }

    [Fact]
    public async Task Referencia_no_primeiro_dia_util_do_mes_de_hoje_nao_projeta_o_resumo_e_nao_exige_os_12_meses_de_ipca()
    {
        // Arrange: referência 03/11 (02/11 é feriado) com fim exclusivo: nenhum dia de novembro entra;
        // só 2 meses de IPCA; outubro é estimado, bruto 1.013,11.
        var asset = CriarAsset(AssetType.Cdb, IndexType.Ipca, rate: 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 1));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 5),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 8, 1), -0.32m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m));

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2026, 11, 3));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.False(response.Data!.IsProjected);
        var item = Assert.Single(response.Data!.Positions).Projection;
        Assert.False(item.IsProjected);
        Assert.True(item.IsEstimated);
        Assert.Equal(1_013.11m, item.GrossAmount);
    }

    [Fact]
    public async Task Sem_data_com_menos_de_12_meses_de_ipca_e_dia_util_no_mes_de_hoje_retorna_400_com_a_mensagem_da_janela()
    {
        // Arrange: 11 meses de IPCA (11/2025 a 09/2026); a janela vai de 10/2025 a 09/2026.
        var asset = CriarAsset(AssetType.Cdb, IndexType.Ipca, rate: 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 3));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 5),
            IpcaDoCaso6().Skip(1).Append(new IndexRate(IndexType.Cdi, new DateOnly(2026, 11, 4), 0.05m)).ToArray());

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

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
        var asset = CriarAsset(AssetType.Cdb, IndexType.Ipca, rate: 6m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 1));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 5),
            IpcaDoCaso6().Where(rate => rate.ReferenceDate != new DateOnly(2026, 2, 1)).ToArray());

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2026, 12, 15));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "A projeção futura do IPCA exige os 12 meses de 2025-10 a 2026-09; há 11, falta 2026-02.",
            response.Message);
    }

    // Fronteira da mudança de comportamento 2 (caso 3-bis): última cotação em 19/11 e 20/11 feriado.
    // Hoje 24/11: a última cotação é de dois dias úteis antes, e nenhum dia sem cotação fica antes do
    // dia útil anterior a hoje (23/11).
    [Fact]
    public async Task Ultima_cotacao_de_cdi_dois_dias_uteis_antes_de_hoje_projeta_o_resumo_na_data_informada()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 24), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2026, 11, 26));

        // Assert: os mesmos valores do caso 1.
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.IsProjected);
        var item = Assert.Single(response.Data!.Positions).Projection;
        Assert.True(item.IsProjected);
        Assert.Equal(1_003.56m, item.GrossAmount);
        Assert.Equal(1_000.94m, item.NetAmount);
    }

    // Hoje 25/11: 23/11 ficou sem cotação antes do dia útil anterior a hoje (24/11).
    [Fact]
    public async Task Ultima_cotacao_de_cdi_tres_dias_uteis_antes_de_hoje_retorna_400_com_a_posicao_o_titulo_e_o_aviso_da_importacao()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb, IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2026, 11, 16));
        var useCase = CriarCasoDe2026(
            new[] { position }, new[] { asset }, new DateOnly(2026, 11, 25), CdiDoCaso1());

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            $"Posição {position.Id} (título 'Título Teste'): Cotação de CDI ausente para 2026-11-23. "
            + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.",
            response.Message);
    }

    [Fact]
    public async Task Itens_do_resumo_levam_a_proxima_faixa_de_ir_e_o_fim_do_iof_de_cada_titulo()
    {
        // Arrange: caso 16 da spec da F13, referência 2025-04-22. Dois aportes de CDB em 2025-04-01
        // (próxima faixa 20,0 em 2025-09-29, IOF zera em 2025-05-01) e uma LCI em 2025-04-02 (isenta
        // de IR, sem próxima faixa; IOF zera em 2025-05-02).
        var cdb = CriarAsset(AssetType.Cdb, name: "CDB A");
        var lci = CriarAsset(AssetType.Lci, name: "LCI B");
        var lciPosition = new Position(lci, 1_500m, new DateOnly(2025, 4, 2));
        var firstCdb = new Position(cdb, 1_000m, new DateOnly(2025, 4, 1));
        var secondCdb = new Position(cdb, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { lciPosition, firstCdb, secondCdb }, new[] { cdb, lci });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var lciItem = Assert.Single(response.Data!.Positions, p => p.Projection.PositionId == lciPosition.Id).Projection;
        Assert.Null(lciItem.NextTaxRate);
        Assert.Null(lciItem.NextTaxRateDate);
        Assert.Equal(new DateOnly(2025, 5, 2), lciItem.IofFreeDate);
        var cdbItems = response.Data!.Positions.Where(p => p.Projection.AssetId == cdb.Id).ToList();
        Assert.Equal(2, cdbItems.Count);
        Assert.All(
            cdbItems,
            item =>
            {
                Assert.Equal(20.0m, item.Projection.NextTaxRate);
                Assert.Equal(new DateOnly(2025, 9, 29), item.Projection.NextTaxRateDate);
                Assert.Equal(new DateOnly(2025, 5, 1), item.Projection.IofFreeDate);
            });
    }

    [Fact]
    public async Task Sem_data_o_aporte_posterior_a_data_padrao_recebe_aporte_mais_181_e_aporte_mais_30()
    {
        // Arrange: data padrão 2025-04-22 (CDI até 2025-04-17). O CDB aportado em 2025-05-05 é projetado
        // na própria data do aporte (d = 0), mas as datas contam a partir dele: 2025-11-02 e 2025-06-04.
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(
            new[] { position },
            new[] { asset },
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        var item = Assert.Single(response.Data!.Positions).Projection;
        Assert.Equal(0, item.ElapsedDays);
        Assert.Equal(20.0m, item.NextTaxRate);
        Assert.Equal(new DateOnly(2025, 11, 2), item.NextTaxRateDate);
        Assert.Equal(new DateOnly(2025, 6, 4), item.IofFreeDate);
    }

    [Fact]
    public async Task Por_titulo_com_data_informada_agrupa_na_ordem_dos_itens_com_os_totais_do_caso_16()
    {
        // Arrange: caso 16 da spec da F13. A (CDB, dois aportes de R$ 1.000 em 01/04/2025) e B (LCI,
        // R$ 1.500 em 02/04/2025), referência 22/04/2025. B foi aportado depois, então vem primeiro em
        // Positions e em ByAsset.
        var cdb = CriarAsset(AssetType.Cdb, name: "CDB A");
        var lci = CriarAsset(AssetType.Lci, name: "LCI B");
        var positions = CriarAportesDoCaso16(cdb, lci);
        var useCase = CriarCaso(positions, new[] { cdb, lci });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        AssertTotaisPorTituloDoCaso16(data, lci, cdb);
        AssertFechamento(data);
    }

    [Fact]
    public async Task Por_titulo_sem_data_informada_usa_a_data_padrao_e_tem_os_totais_do_caso_16()
    {
        // Arrange: a última cotação de CDI é quinta, 2025-04-17; 18/04 e 21/04 são feriados, então a
        // data padrão é terça, 2025-04-22, a mesma referência do teste anterior.
        var cdb = CriarAsset(AssetType.Cdb, name: "CDB A");
        var lci = CriarAsset(AssetType.Lci, name: "LCI B");
        var positions = CriarAportesDoCaso16(cdb, lci);
        var useCase = CriarCaso(
            positions, new[] { cdb, lci }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        Assert.Equal(new DateOnly(2025, 4, 22), data.ReferenceDate);
        AssertTotaisPorTituloDoCaso16(data, lci, cdb);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Por_titulo_tem_os_mesmos_totais_da_projecao_do_titulo_na_mesma_data(bool informarData)
    {
        // Arrange
        var cdb = CriarAsset(AssetType.Cdb, name: "CDB A");
        var lci = CriarAsset(AssetType.Lci, name: "LCI B");
        var positions = CriarAportesDoCaso16(cdb, lci);
        var cdi = new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m);
        var summaryUseCase = CriarCaso(positions, new[] { cdb, lci }, cdi);
        var assetUseCase = new GetAssetProjectionUseCase(
            new FixedIncomeAssetRepositoryStub(new[] { cdb, lci }),
            new PositionRepositoryStub(positions),
            new PositionProjector(CriarLoader(cdi), new TimeProviderStub(Now)));
        DateOnly? referenceDate = informarData ? new DateOnly(2025, 4, 22) : null;

        // Act
        var summary = (await summaryUseCase.ExecuteAsync(referenceDate)).Data!;
        var cdbProjection = (await assetUseCase.ExecuteAsync(cdb.Id, referenceDate)).Data!;
        var lciProjection = (await assetUseCase.ExecuteAsync(lci.Id, referenceDate)).Data!;

        // Assert
        AssertTotaisIguaisAProjecaoDoTitulo(summary.ByAsset.Single(item => item.AssetId == cdb.Id), cdbProjection);
        AssertTotaisIguaisAProjecaoDoTitulo(summary.ByAsset.Single(item => item.AssetId == lci.Id), lciProjection);
    }

    [Fact]
    public async Task Por_titulo_nao_inclui_titulo_cujos_aportes_ficam_todos_depois_da_data_informada()
    {
        // Arrange: o aporte do segundo título é de 25/04/2025, depois da referência 22/04/2025, e fica
        // fora dos itens, então o título não tem posição em Positions nem item em ByAsset.
        var included = CriarAsset(AssetType.Cdb, name: "CDB A");
        var later = CriarAsset(AssetType.Lci, name: "LCI C");
        var positions = new[]
        {
            new Position(later, 500m, new DateOnly(2025, 4, 25)),
            new Position(included, 1_000m, new DateOnly(2025, 4, 1))
        };
        var useCase = CriarCaso(positions, new[] { included, later });

        // Act
        var response = await useCase.ExecuteAsync(new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var item = Assert.Single(response.Data!.ByAsset);
        Assert.Equal(included.Id, item.AssetId);
        Assert.Equal(1, item.PositionCount);
        Assert.DoesNotContain(response.Data.ByAsset, byAsset => byAsset.AssetId == later.Id);
    }

    [Fact]
    public async Task Por_titulo_e_vazio_com_a_carteira_vazia()
    {
        // Arrange
        var useCase = CriarCaso(Array.Empty<Position>(), Array.Empty<FixedIncomeAsset>());

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Data!.ByAsset);
    }

    // Do mais recente para o mais antigo, como o repositório entrega (RN-14): B, A, A.
    private static Position[] CriarAportesDoCaso16(FixedIncomeAsset cdb, FixedIncomeAsset lci) => new[]
    {
        new Position(lci, 1_500m, new DateOnly(2025, 4, 2)),
        new Position(cdb, 1_000m, new DateOnly(2025, 4, 1)),
        new Position(cdb, 1_000m, new DateOnly(2025, 4, 1))
    };

    // Caso 16: byAsset[0] = B (LCI), byAsset[1] = A (CDB), cada total conferido, e a raiz.
    private static void AssertTotaisPorTituloDoCaso16(
        PortfolioSummaryResponse data, FixedIncomeAsset lci, FixedIncomeAsset cdb)
    {
        Assert.Equal(2, data.ByAsset.Count);

        var b = data.ByAsset[0];
        Assert.Equal(lci.Id, b.AssetId);
        Assert.Equal(1, b.PositionCount);
        Assert.Equal(1_500.00m, b.TotalInvestedAmount);
        Assert.Equal(1_506.82m, b.TotalGrossAmount);
        Assert.Equal(6.82m, b.TotalGrossYield);
        Assert.Equal(0.00m, b.TotalTaxAmount);
        Assert.Equal(2.25m, b.TotalIofAmount);
        Assert.Equal(1_504.57m, b.TotalNetAmount);
        Assert.Equal(4.57m, b.TotalNetYield);

        var a = data.ByAsset[1];
        Assert.Equal(cdb.Id, a.AssetId);
        Assert.Equal(2, a.PositionCount);
        Assert.Equal(2_000.00m, a.TotalInvestedAmount);
        Assert.Equal(2_009.86m, a.TotalGrossAmount);
        Assert.Equal(9.86m, a.TotalGrossYield);
        Assert.Equal(1.56m, a.TotalTaxAmount);
        Assert.Equal(2.96m, a.TotalIofAmount);
        Assert.Equal(2_005.34m, a.TotalNetAmount);
        Assert.Equal(5.34m, a.TotalNetYield);

        Assert.Equal(3, data.PositionCount);
        Assert.Equal(3_500.00m, data.TotalInvestedAmount);
        Assert.Equal(3_516.68m, data.TotalGrossAmount);
        Assert.Equal(16.68m, data.TotalGrossYield);
        Assert.Equal(1.56m, data.TotalTaxAmount);
        Assert.Equal(5.21m, data.TotalIofAmount);
        Assert.Equal(3_509.91m, data.TotalNetAmount);
        Assert.Equal(9.91m, data.TotalNetYield);

        // Cada total do título é a soma dos itens dele em Positions.
        foreach (var item in data.ByAsset)
        {
            var own = data.Positions.Where(p => p.Asset.Id == item.AssetId).Select(p => p.Projection).ToList();
            Assert.Equal(own.Count, item.PositionCount);
            Assert.Equal(own.Sum(p => p.InvestedAmount), item.TotalInvestedAmount);
            Assert.Equal(own.Sum(p => p.GrossAmount), item.TotalGrossAmount);
            Assert.Equal(own.Sum(p => p.GrossYield), item.TotalGrossYield);
            Assert.Equal(own.Sum(p => p.TaxAmount), item.TotalTaxAmount);
            Assert.Equal(own.Sum(p => p.IofAmount), item.TotalIofAmount);
            Assert.Equal(own.Sum(p => p.NetAmount), item.TotalNetAmount);
            Assert.Equal(own.Sum(p => p.NetYield), item.TotalNetYield);
        }
    }

    private static void AssertTotaisIguaisAProjecaoDoTitulo(
        PortfolioSummaryResponse.AssetSummaryResponse item, AssetProjectionResponse projection)
    {
        Assert.Equal(projection.AssetId, item.AssetId);
        Assert.Equal(projection.PositionCount, item.PositionCount);
        Assert.Equal(projection.TotalInvestedAmount, item.TotalInvestedAmount);
        Assert.Equal(projection.TotalGrossAmount, item.TotalGrossAmount);
        Assert.Equal(projection.TotalGrossYield, item.TotalGrossYield);
        Assert.Equal(projection.TotalTaxAmount, item.TotalTaxAmount);
        Assert.Equal(projection.TotalIofAmount, item.TotalIofAmount);
        Assert.Equal(projection.TotalNetAmount, item.TotalNetAmount);
        Assert.Equal(projection.TotalNetYield, item.TotalNetYield);
    }

    private static GetPortfolioSummaryUseCase CriarCasoComCalendarioVazio(
        IEnumerable<Position> positions, IEnumerable<FixedIncomeAsset> assets, params IndexRate[] rates)
    {
        return new GetPortfolioSummaryUseCase(
            new PositionRepositoryStub(positions),
            new FixedIncomeAssetRepositoryStub(assets),
            new PositionProjector(
                new MarketDataLoader(
                    new HolidayRepositoryStub(Array.Empty<Holiday>()),
                    new IndexRateRepositoryStub(rates),
                    new TimeProviderStub(Now)),
                new TimeProviderStub(Now)));
    }

    private static GetPortfolioSummaryUseCase CriarCaso(
        IEnumerable<Position> positions, IEnumerable<FixedIncomeAsset> assets, params IndexRate[] rates)
    {
        return CriarCaso(positions, assets, new TimeProviderStub(Now), rates);
    }

    private static GetPortfolioSummaryUseCase CriarCaso(
        IEnumerable<Position> positions, IEnumerable<FixedIncomeAsset> assets, TimeProvider clock, params IndexRate[] rates)
    {
        return new GetPortfolioSummaryUseCase(
            new PositionRepositoryStub(positions),
            new FixedIncomeAssetRepositoryStub(assets),
            new PositionProjector(CriarLoader(rates), clock));
    }

    private static MarketDataLoader CriarLoader(params IndexRate[] rates) =>
        new(new HolidayRepositoryStub(TestMarket.Holidays()), new IndexRateRepositoryStub(rates), new TimeProviderStub(Now));

    private static FixedIncomeAsset CriarAsset(
        AssetType assetType, IndexType indexType = IndexType.PreFixed, decimal rate = 10m,
        string name = "Título Teste", string issuer = "Emissor Teste")
    {
        var start = new DateOnly(2025, 1, 1);
        return new FixedIncomeAsset(name, issuer, assetType, indexType, rate, start, start.AddYears(2));
    }

    // Soma de cada campo monetário dos itens contra o total correspondente (RN-07): a lista única
    // de projeções alimenta os totais e os itens, então a soma é exata.
    private static void AssertFechamento(PortfolioSummaryResponse data)
    {
        Assert.Equal(data.PositionCount, data.Positions.Count);
        Assert.Equal(data.TotalInvestedAmount, data.Positions.Sum(p => p.Projection.InvestedAmount));
        Assert.Equal(data.TotalGrossAmount, data.Positions.Sum(p => p.Projection.GrossAmount));
        Assert.Equal(data.TotalGrossYield, data.Positions.Sum(p => p.Projection.GrossYield));
        Assert.Equal(data.TotalTaxAmount, data.Positions.Sum(p => p.Projection.TaxAmount));
        Assert.Equal(data.TotalIofAmount, data.Positions.Sum(p => p.Projection.IofAmount));
        Assert.Equal(data.TotalNetAmount, data.Positions.Sum(p => p.Projection.NetAmount));
    }

    // Os feriados de 2026 que caem nos intervalos do caso IPCA+ (07/09 e 12/10).
    private static GetPortfolioSummaryUseCase CriarCasoComFeriadosDe2026(
        IEnumerable<Position> positions, IEnumerable<FixedIncomeAsset> assets, params IndexRate[] rates)
    {
        var holidays = TestMarket.Holidays()
            .Append(new Holiday(new DateOnly(2026, 9, 7), "Feriado nacional"))
            .Append(new Holiday(new DateOnly(2026, 10, 12), "Feriado nacional"))
            .ToList();

        return new GetPortfolioSummaryUseCase(
            new PositionRepositoryStub(positions),
            new FixedIncomeAssetRepositoryStub(assets),
            new PositionProjector(
                new MarketDataLoader(
                new HolidayRepositoryStub(holidays),
                new IndexRateRepositoryStub(rates),
                // Novembro/2026: outubro está encerrado e não divulgado, então é estimado (RN-10) e não
                // projetado (RN-12); com o relógio do arquivo (2025-12-01) virava projetado.
                new TimeProviderStub(new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero))),
                new TimeProviderStub(Now)));
    }

    // Calendário com os feriados de 2026 e relógio fixo no dia dado: o mesmo TimeProvider no carregador
    // (fronteiras da RN-12) e no caso de uso (data padrão).
    private static GetPortfolioSummaryUseCase CriarCasoDe2026(
        IEnumerable<Position> positions, IEnumerable<FixedIncomeAsset> assets, DateOnly today, params IndexRate[] rates)
    {
        var clock = new TimeProviderStub(new DateTimeOffset(today.Year, today.Month, today.Day, 15, 0, 0, TimeSpan.Zero));
        var loader = new MarketDataLoader(
            new HolidayRepositoryStub(TestMarket.HolidaysWith2026()), new IndexRateRepositoryStub(rates), clock);

        return new GetPortfolioSummaryUseCase(
            new PositionRepositoryStub(positions), new FixedIncomeAssetRepositoryStub(assets), new PositionProjector(loader, clock));
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
        private readonly IEnumerable<FixedIncomeAsset> _assets;

        public int GetAllCallCount { get; private set; }

        public FixedIncomeAssetRepositoryStub(IEnumerable<FixedIncomeAsset> assets)
        {
            _assets = assets;
        }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync()
        {
            GetAllCallCount++;
            return Task.FromResult(_assets);
        }

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) =>
            Task.FromResult(_assets.SingleOrDefault(asset => asset.Id == id));

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset) => Task.CompletedTask;
    }

    private sealed class PositionRepositoryStub : IPositionRepository
    {
        private readonly IEnumerable<Position> _positions;

        public PositionRepositoryStub(IEnumerable<Position> positions)
        {
            _positions = positions;
        }

        public Task<IEnumerable<Position>> GetAllAsync() => Task.FromResult(_positions);

        public Task<Position?> GetByIdAsync(Guid id) => Task.FromResult<Position?>(null);

        public Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId) =>
            Task.FromResult(_positions.Where(p => p.AssetId == assetId).AsEnumerable());

        public Task AddAsync(Position position) => Task.CompletedTask;
    }
}
