using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.Services;

public class MarketDataLoaderTests
{
    [Fact]
    public async Task Carrega_o_calendario_a_partir_dos_feriados_cadastrados()
    {
        // Arrange
        var loader = CriarLoader(Feriados2026());

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        // Assert: Tiradentes (terça, 21/04/2026) não é dia útil; 22/04 é.
        Assert.False(market.Calendar.IsBusinessDay(new DateOnly(2026, 4, 21)));
        Assert.True(market.Calendar.IsBusinessDay(new DateOnly(2026, 4, 22)));
    }

    [Fact]
    public async Task Carrega_o_cdi_do_periodo_com_as_duas_pontas_inclusivas_e_alem_delas_so_a_ultima_cotacao()
    {
        // Arrange
        var loader = CriarLoader(
            Feriados2026(),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 11), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 14), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 15), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 16), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 17), 0.050788m));

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 15));

        // Assert: 11/09 e 16/09 estão fora do período e não são a última cotação; a última (17/09)
        // entra sempre, pela D-1.
        Assert.Null(market.GetCdi(new DateOnly(2026, 9, 11)));
        Assert.Equal(0.051660m, market.GetCdi(new DateOnly(2026, 9, 14)));
        Assert.Equal(0.051660m, market.GetCdi(new DateOnly(2026, 9, 15)));
        Assert.Null(market.GetCdi(new DateOnly(2026, 9, 16)));
        Assert.Equal(0.050788m, market.GetCdi(new DateOnly(2026, 9, 17)));
    }

    [Fact]
    public async Task Carrega_o_ipca_do_mes_em_que_o_periodo_comeca_mesmo_que_ele_seja_datado_antes_do_inicio()
    {
        // Arrange: o IPCA de abril é datado em 01/04, antes do início do período (15/04).
        var loader = CriarLoader(
            Feriados2026(),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 4, 1), 0.67m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 5, 1), 0.58m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 6, 1), 0.16m));

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 4, 15), new DateOnly(2026, 6, 15));

        // Assert
        Assert.Equal(0.67m, market.GetIpca(new DateOnly(2026, 4, 15)));
        Assert.Equal(0.58m, market.GetIpca(new DateOnly(2026, 5, 1)));
        Assert.Equal(0.16m, market.GetIpca(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task Inclui_o_ultimo_ipca_publicado_anterior_ao_periodo_para_a_estimativa()
    {
        // Arrange: o período começa em 01/10 e o último IPCA publicado é o de agosto, antes dele.
        var loader = CriarLoader(
            Feriados2026(),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 7, 1), 0.07m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 8, 1), -0.32m));

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));

        // Assert
        Assert.Null(market.GetIpca(new DateOnly(2026, 10, 1)));
        Assert.Equal((new DateOnly(2026, 8, 1), -0.32m), market.LastPublishedIpca);
    }

    [Fact]
    public async Task Sem_ipca_publicado_em_nenhuma_data_a_serie_fica_vazia()
    {
        // Arrange
        var loader = CriarLoader(Feriados2026());

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));

        // Assert
        Assert.Null(market.LastPublishedIpca);
    }

    [Fact]
    public async Task Ipca_estimado_fora_do_periodo_marca_a_projecao_como_estimada()
    {
        // Arrange: caso 6.3 da spec adaptado. IPCA publicado até 09/2026, período 01/10 a 15/10:
        // 9 dias úteis (12/10 é feriado), fator IPCA 1,0082^(9/21) = 1,003506087 e fator real
        // 1,06^(9/252) = 1,002083199; bruto de R$ 1.000 = 1.005,60.
        var loader = CriarLoader(
            Feriados2026(),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 8, 1), -0.32m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m));
        var asset = new FixedIncomeAsset(
            "CDB IPCA+", "Banco X", AssetType.Cdb, IndexType.Ipca, 6m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2026, 10, 1));

        // Act
        var market = await loader.LoadAsync(position.ApplicationDate, new DateOnly(2026, 10, 15));
        var projection = position.Project(asset, new DateOnly(2026, 10, 15), market);

        // Assert
        Assert.True(projection.IsEstimated);
        Assert.Equal(9, projection.BusinessDays);
        Assert.Equal(1_005.60m, projection.GrossAmount);
    }

    [Fact]
    public async Task Sem_feriados_cadastrados_lanca_excecao_de_dominio()
    {
        // Arrange
        var loader = CriarLoader(Array.Empty<Holiday>());

        // Act
        var act = () => loader.LoadAsync(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        // Assert
        await Assert.ThrowsAsync<DomainException>(act);
    }

    [Fact]
    public async Task Data_padrao_e_o_dia_util_seguinte_a_ultima_cotacao_de_cdi()
    {
        // Arrange: 2026-09-11 é sexta; o dia útil seguinte é segunda, 2026-09-14 (caso 2.1 da spec).
        var loader = CriarLoader(
            Feriados2026(),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 10), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 11), 0.051660m));

        // Act
        var date = await loader.GetDefaultReferenceDateAsync();

        // Assert
        Assert.Equal(new DateOnly(2026, 9, 14), date);
    }

    [Fact]
    public async Task Data_padrao_pula_feriado_seguinte_a_ultima_cotacao_de_cdi()
    {
        // Arrange: depois de sexta, 04/09/2026, vêm o fim de semana e a segunda 07/09 (feriado);
        // o dia útil seguinte é terça, 08/09.
        var loader = CriarLoader(
            Feriados2026(),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 4), 0.051660m));

        // Act
        var date = await loader.GetDefaultReferenceDateAsync();

        // Assert
        Assert.Equal(new DateOnly(2026, 9, 8), date);
    }

    [Fact]
    public async Task Data_padrao_sem_nenhuma_cotacao_de_cdi_e_nula()
    {
        // Arrange
        var loader = CriarLoader(Feriados2026());

        // Act
        var date = await loader.GetDefaultReferenceDateAsync();

        // Assert
        Assert.Null(date);
    }

    [Fact]
    public async Task A_serie_inclui_a_ultima_cotacao_de_cdi_e_os_12_meses_de_ipca_mesmo_com_o_periodo_longe_deles()
    {
        // Arrange: CDI até 16/09 e IPCA de 07/2025 a 09/2026 (15 meses); o período é de janeiro/2026.
        var loader = CriarLoader(
            Feriados2026(),
            Cdis("2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16").Concat(IpcaMensal("2025-07-01", 15)).ToArray());

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20));

        // Assert: o CDI traz só a última cotação (D-1); o IPCA traz os 12 meses de 10/2025 a 09/2026
        // e nada de 07/2025 a 09/2025, fora da janela e do período.
        Assert.Null(market.GetCdi(new DateOnly(2026, 9, 15)));
        Assert.NotNull(market.GetCdi(new DateOnly(2026, 9, 16)));
        Assert.Equal((new DateOnly(2026, 9, 16), 0.051660m), market.LastCdi);
        Assert.Null(market.GetIpca(new DateOnly(2025, 9, 1)));
        Assert.NotNull(market.GetIpca(new DateOnly(2025, 10, 1)));
        Assert.NotNull(market.GetIpca(new DateOnly(2026, 1, 1)));
        Assert.NotNull(market.GetIpca(new DateOnly(2026, 9, 1)));
        Assert.Equal(new DateOnly(2026, 9, 1), market.LastPublishedIpca!.Value.Month);
        Assert.True(market.TryGetProjectedIpca(out _, out var failure), failure);
    }

    [Fact]
    public async Task Periodo_sobreposto_a_janela_e_a_ultima_cotacao_nao_gera_cotacao_duplicada()
    {
        // Arrange: o período cobre a última cotação de CDI e parte da janela de IPCA; sem a
        // deduplicação o MarketData lançaria DomainException de cotação duplicada.
        var loader = CriarLoader(
            Feriados2026(),
            Cdis("2026-09-14", "2026-09-15", "2026-09-16").Concat(IpcaMensal("2025-10-01", 12)).ToArray());

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 3, 15), new DateOnly(2026, 9, 16));

        // Assert
        Assert.Equal(0.051660m, market.GetCdi(new DateOnly(2026, 9, 16)));
        Assert.Equal(0.30m, market.GetIpca(new DateOnly(2026, 3, 1)));
        Assert.Equal(0.30m, market.GetIpca(new DateOnly(2025, 10, 1)));
        Assert.True(market.TryGetProjectedIpca(out _, out var failure), failure);
    }

    [Fact]
    public async Task O_dia_de_hoje_vem_do_time_provider_no_fuso_de_brasilia()
    {
        // Arrange: 06/11/2026 01:30 UTC é 05/11/2026 22:30 em Brasília (UTC-3).
        var clock = new TimeProviderStub(new DateTimeOffset(2026, 11, 6, 1, 30, 0, TimeSpan.Zero));
        var loader = CriarLoader(clock, Feriados2026());

        // Act
        var market = await loader.LoadAsync(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        // Assert
        Assert.Equal(new DateOnly(2026, 11, 5), market.Today);
    }

    private static IndexRate[] Cdis(params string[] dates) =>
        dates.Select(date => new IndexRate(IndexType.Cdi, DateOnly.Parse(date), 0.051660m)).ToArray();

    // IPCA mensal fixo de 0,30 a partir do mês informado, para testar só o que entra na série.
    private static IndexRate[] IpcaMensal(string firstMonth, int months)
    {
        var start = DateOnly.Parse(firstMonth);

        return Enumerable.Range(0, months)
            .Select(i => new IndexRate(IndexType.Ipca, start.AddMonths(i), 0.30m))
            .ToArray();
    }

    // Relógio padrão: 05/11/2026, depois dos meses de IPCA estimados pelos testes (até outubro/2026).
    private static MarketDataLoader CriarLoader(IReadOnlyList<Holiday> holidays, params IndexRate[] rates) =>
        CriarLoader(new TimeProviderStub(new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero)), holidays, rates);

    private static MarketDataLoader CriarLoader(
        TimeProvider clock, IReadOnlyList<Holiday> holidays, params IndexRate[] rates) =>
        new(new HolidayRepositoryStub(holidays), new IndexRateRepositoryStub(rates), clock);

    private static IReadOnlyList<Holiday> Feriados2026()
    {
        // Feriados nacionais ANBIMA de 2026 (seção "Feriados ANBIMA" da spec).
        string[] dates =
        [
            "2026-01-01", "2026-02-16", "2026-02-17", "2026-04-03", "2026-04-21", "2026-05-01",
            "2026-06-04", "2026-09-07", "2026-10-12", "2026-11-02", "2026-11-15", "2026-11-20",
            "2026-12-25"
        ];

        return dates.Select(date => new Holiday(DateOnly.Parse(date), "Feriado nacional")).ToList();
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
}
