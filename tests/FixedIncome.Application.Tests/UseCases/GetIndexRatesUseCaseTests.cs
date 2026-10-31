using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetIndexRatesUseCaseTests
{
    [Fact]
    public async Task Periodo_valido_retorna_200_com_as_cotacoes_do_indexador_no_periodo()
    {
        // Arrange
        var repository = new IndexRateRepositoryStub(
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 11), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 14), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 17), 0.050788m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m));
        var useCase = new GetIndexRatesUseCase(repository);

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Cdi, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 17));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var rates = response.Data!.ToList();
        Assert.Equal(2, rates.Count);
        Assert.All(rates, rate => Assert.Equal(IndexType.Cdi, rate.IndexType));
        Assert.Equal(new DateOnly(2026, 9, 14), rates[0].ReferenceDate);
        Assert.Equal(0.051660m, rates[0].Value);
        Assert.Equal(new DateOnly(2026, 9, 17), rates[1].ReferenceDate);
        Assert.Equal(0.050788m, rates[1].Value);
    }

    [Fact]
    public async Task Periodo_sem_cotacoes_retorna_200_com_lista_vazia()
    {
        // Arrange
        var useCase = new GetIndexRatesUseCase(new IndexRateRepositoryStub());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Ipca, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task Data_inicial_igual_a_data_final_e_aceita()
    {
        // Arrange
        var useCase = new GetIndexRatesUseCase(
            new IndexRateRepositoryStub(new IndexRate(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m)));

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Ipca, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Data_inicial_posterior_a_data_final_retorna_400()
    {
        // Arrange
        var useCase = new GetIndexRatesUseCase(new IndexRateRepositoryStub());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Cdi, new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 14));

        // Assert
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Indexador_pre_fixado_retorna_400_porque_nao_tem_cotacao()
    {
        // Arrange
        var useCase = new GetIndexRatesUseCase(new IndexRateRepositoryStub());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.PreFixed, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        // Assert
        Assert.Equal(400, response.StatusCode);
    }

    private sealed class IndexRateRepositoryStub : IIndexRateRepository
    {
        private readonly IReadOnlyList<IndexRate> _rates;

        public IndexRateRepositoryStub(params IndexRate[] rates)
        {
            _rates = rates;
        }

        public Task<IEnumerable<IndexRate>> GetByIndexTypeAndPeriodAsync(IndexType indexType, DateOnly from, DateOnly to) =>
            Task.FromResult(_rates
                .Where(r => r.IndexType == indexType && r.ReferenceDate >= from && r.ReferenceDate <= to)
                .OrderBy(r => r.ReferenceDate)
                .AsEnumerable());

        public Task<IndexRate?> GetLastBeforeAsync(IndexType indexType, DateOnly date) =>
            Task.FromResult<IndexRate?>(null);

        public Task<DateOnly?> GetLastReferenceDateAsync(IndexType indexType) => Task.FromResult<DateOnly?>(null);

        public Task UpsertAsync(IEnumerable<IndexRate> rates) => Task.CompletedTask;
    }
}
