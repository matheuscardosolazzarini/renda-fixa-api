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
            new MarketDataLoader(new HolidayRepositoryStub(Array.Empty<Holiday>()), new IndexRateRepositoryStub(Array.Empty<IndexRate>())),
            new TimeProviderStub(Now));

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
            CriarLoader(),
            new TimeProviderStub(Now));

        // Act
        var response = await useCase.ExecuteAsync(asset.IssueDate.AddDays(30));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, assetRepository.GetAllCallCount);
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
            CriarLoader(rates),
            clock);
    }

    private static MarketDataLoader CriarLoader(params IndexRate[] rates) =>
        new(new HolidayRepositoryStub(TestMarket.Holidays()), new IndexRateRepositoryStub(rates));

    private static FixedIncomeAsset CriarAsset(
        AssetType assetType, IndexType indexType = IndexType.PreFixed, decimal rate = 10m)
    {
        var start = new DateOnly(2025, 1, 1);
        return new FixedIncomeAsset(
            "Título Teste", "Emissor Teste", assetType, indexType, rate, start, start.AddYears(2));
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

        public Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId) => Task.FromResult(_positions);

        public Task AddAsync(Position position) => Task.CompletedTask;
    }
}
