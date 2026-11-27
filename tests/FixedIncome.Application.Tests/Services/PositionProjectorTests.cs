using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Application.Tests.TestData;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.Services;

// Teste 28 da SPEC-F12: a classe da D-11 produz o que o resumo produzia, para os cenários de data.
// Os valores esperados são os mesmos já conferidos à mão nos testes do resumo (PreFixed 10% a.a.,
// R$ 1.000 em 2025-04-01 até 2025-04-22: bruto 1.004,93).
public class PositionProjectorTests
{
    // Relógio bem distante das datas dos testes: se a data de hoje fosse usada onde deve valer a
    // última cotação de CDI, os valores esperados não bateriam.
    private static readonly DateTimeOffset Now = new(2025, 12, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Data_informada_e_a_data_resolvida_e_a_usada_na_projecao()
    {
        // Arrange
        var asset = CriarAsset();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var projector = CriarProjector();

        // Act
        var result = await projector.ProjectAsync(new[] { position }, Dicionario(asset), new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(new DateOnly(2025, 4, 22), result.ReferenceDate);
        var item = Assert.Single(result.Items);
        Assert.Same(position, item.Position);
        Assert.Same(asset, item.Asset);
        Assert.Equal(new DateOnly(2025, 4, 22), item.Date);
        Assert.Equal(1_004.93m, item.Projection.GrossAmount);
    }

    [Fact]
    public async Task Aporte_posterior_a_data_informada_fica_fora_e_o_aporte_na_propria_data_entra()
    {
        // Arrange: R-02, com fronteira inclusiva. O aporte de 22/04 entra (rende zero); o de 23/04 sai.
        var asset = CriarAsset();
        var onDate = new Position(asset, 1_000m, new DateOnly(2025, 4, 22));
        var after = new Position(asset, 500m, new DateOnly(2025, 4, 23));
        var projector = CriarProjector();

        // Act
        var result = await projector.ProjectAsync(new[] { onDate, after }, Dicionario(asset), new DateOnly(2025, 4, 22));

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Same(onDate, item.Position);
        Assert.Equal(1_000m, item.Projection.GrossAmount);
    }

    [Fact]
    public async Task Sem_data_usa_o_dia_util_seguinte_a_ultima_cotacao_de_cdi()
    {
        // Arrange: última cotação de CDI na quinta, 2025-04-17; 18/04 e 21/04 são feriados, então a
        // data padrão é terça, 2025-04-22 (e não a de hoje do relógio).
        var asset = CriarAsset();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var projector = CriarProjector(rates: new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var result = await projector.ProjectAsync(new[] { position }, Dicionario(asset), referenceDate: null);

        // Assert
        Assert.Equal(new DateOnly(2025, 4, 22), result.ReferenceDate);
        var item = Assert.Single(result.Items);
        Assert.Equal(new DateOnly(2025, 4, 22), item.Date);
        Assert.Equal(1_004.93m, item.Projection.GrossAmount);
    }

    [Fact]
    public async Task Sem_data_e_sem_cotacao_de_cdi_usa_a_data_de_hoje_em_brasilia()
    {
        // Arrange: 22h37 de 22/04 em Brasília já é 01h37 de 23/04 em UTC; vale a data de Brasília (R-01).
        var asset = CriarAsset();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var clock = new TimeProviderStub(new DateTimeOffset(2025, 4, 23, 1, 37, 0, TimeSpan.Zero));
        var projector = CriarProjector(clock);

        // Act
        var result = await projector.ProjectAsync(new[] { position }, Dicionario(asset), referenceDate: null);

        // Assert
        Assert.Equal(new DateOnly(2025, 4, 22), result.ReferenceDate);
        Assert.Equal(1_004.93m, Assert.Single(result.Items).Projection.GrossAmount);
    }

    [Fact]
    public async Task Sem_data_o_aporte_posterior_a_data_padrao_e_projetado_na_propria_data_e_a_raiz_segue_a_padrao()
    {
        // Arrange: data padrão 2025-04-22; o aporte em CDI é de 2025-05-05 e não há cotação para ele.
        var asset = CriarAsset(IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var projector = CriarProjector(rates: new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var result = await projector.ProjectAsync(new[] { position }, Dicionario(asset), referenceDate: null);

        // Assert
        Assert.Equal(new DateOnly(2025, 4, 22), result.ReferenceDate);
        var item = Assert.Single(result.Items);
        Assert.Equal(new DateOnly(2025, 5, 5), item.Date);
        Assert.Equal(1_000m, item.Projection.GrossAmount);
        Assert.Equal(0m, item.Projection.GrossYield);
    }

    [Fact]
    public async Task Devolve_os_itens_na_ordem_de_entrada()
    {
        // Arrange: a ordem de entrada não é a cronológica; a saída não pode reordenar (D-6).
        var asset = CriarAsset();
        var recent = new Position(asset, 500m, new DateOnly(2025, 4, 10));
        var old = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var projector = CriarProjector();

        // Act
        var result = await projector.ProjectAsync(new[] { recent, old }, Dicionario(asset), new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(new[] { recent, old }, result.Items.Select(i => i.Position).ToArray());
    }

    [Fact]
    public async Task Carrega_o_mercado_uma_unica_vez_para_varios_aportes()
    {
        // Arrange
        var asset = CriarAsset();
        var first = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var second = new Position(asset, 500m, new DateOnly(2025, 4, 10));
        var holidays = new HolidayRepositoryStub(TestMarket.Holidays());
        var projector = CriarProjector(holidays: holidays);

        // Act
        await projector.ProjectAsync(new[] { first, second }, Dicionario(asset), new DateOnly(2025, 4, 22));

        // Assert: cada carregamento lê o calendário uma vez.
        Assert.Equal(1, holidays.GetAllCallCount);
    }

    [Fact]
    public async Task Sem_aportes_devolve_lista_vazia_com_a_data_informada_sem_carregar_o_mercado()
    {
        // Arrange
        var holidays = new HolidayRepositoryStub(TestMarket.Holidays());
        var projector = CriarProjector(holidays: holidays);

        // Act
        var result = await projector.ProjectAsync(
            Array.Empty<Position>(), new Dictionary<Guid, FixedIncomeAsset>(), new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(new DateOnly(2025, 4, 22), result.ReferenceDate);
        Assert.Empty(result.Items);
        Assert.Equal(0, holidays.GetAllCallCount);
    }

    [Fact]
    public async Task Todos_os_aportes_posteriores_a_data_informada_deixam_a_lista_vazia_sem_carregar_o_mercado()
    {
        // Arrange
        var asset = CriarAsset();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var holidays = new HolidayRepositoryStub(TestMarket.Holidays());
        var projector = CriarProjector(holidays: holidays);

        // Act
        var result = await projector.ProjectAsync(new[] { position }, Dicionario(asset), new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(new DateOnly(2025, 4, 22), result.ReferenceDate);
        Assert.Empty(result.Items);
        Assert.Equal(0, holidays.GetAllCallCount);
    }

    [Fact]
    public async Task Sem_aportes_e_sem_data_resolve_a_data_padrao_mesmo_assim()
    {
        // Arrange: a data do resultado nunca é nula, inclusive sem aportes.
        var projector = CriarProjector(rates: new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var result = await projector.ProjectAsync(
            Array.Empty<Position>(), new Dictionary<Guid, FixedIncomeAsset>(), referenceDate: null);

        // Assert
        Assert.Equal(new DateOnly(2025, 4, 22), result.ReferenceDate);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Cotacao_de_cdi_ausente_lanca_domain_exception_com_a_posicao_o_titulo_e_o_aviso_da_importacao()
    {
        // Arrange: só há CDI de 01/04; falta 02/04.
        var asset = CriarAsset(IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var projector = CriarProjector(rates: new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 1), 0.05m));

        // Act
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => projector.ProjectAsync(new[] { position }, Dicionario(asset), new DateOnly(2025, 4, 4)));

        // Assert
        Assert.Equal(
            $"Posição {position.Id} (título 'Título Teste'): Cotação de CDI ausente para 2025-04-02. "
            + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.",
            exception.Message);
    }

    [Fact]
    public async Task Outro_erro_de_dominio_segue_com_a_mensagem_original()
    {
        // Arrange: sem feriados o carregamento de mercado lança DomainException; sem prefixo da R-10.
        var asset = CriarAsset();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var projector = CriarProjector(holidays: new HolidayRepositoryStub(Array.Empty<Holiday>()));

        // Act
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => projector.ProjectAsync(new[] { position }, Dicionario(asset), new DateOnly(2025, 4, 22)));

        // Assert
        Assert.Equal("O calendário precisa de ao menos um feriado para definir o período coberto.", exception.Message);
    }

    private static PositionProjector CriarProjector(
        TimeProvider? clock = null, HolidayRepositoryStub? holidays = null, params IndexRate[] rates)
    {
        clock ??= new TimeProviderStub(Now);
        holidays ??= new HolidayRepositoryStub(TestMarket.Holidays());

        return new PositionProjector(
            new MarketDataLoader(holidays, new IndexRateRepositoryStub(rates), new TimeProviderStub(Now)), clock);
    }

    private static Dictionary<Guid, FixedIncomeAsset> Dicionario(params FixedIncomeAsset[] assets) =>
        assets.ToDictionary(asset => asset.Id);

    private static FixedIncomeAsset CriarAsset(IndexType indexType = IndexType.PreFixed, decimal rate = 10m)
    {
        var start = new DateOnly(2025, 1, 1);
        return new FixedIncomeAsset(
            "Título Teste", "Emissor Teste", AssetType.Cdb, indexType, rate, start, start.AddYears(2));
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

        public int GetAllCallCount { get; private set; }

        public HolidayRepositoryStub(IEnumerable<Holiday> holidays)
        {
            _holidays = holidays;
        }

        public Task<IEnumerable<Holiday>> GetAllAsync()
        {
            GetAllCallCount++;
            return Task.FromResult(_holidays);
        }
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
}
