using System.Data.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FixedIncome.Infrastructure.Tests.Repositories;

public class IndexRateRepositoryTests : RepositoryTestBase
{
    // O InMemory não executa SQL. Estes testes usam o provedor do PostgreSQL sem banco: os
    // interceptadores impedem a conexão e capturam o comando. Eles provam o SQL enviado; o efeito
    // do ON CONFLICT no banco real é verificado pelo race.sh (ver QA-F7.md) e, na F9, por Testcontainers.
    [Fact]
    public async Task Upsert_grava_todas_as_cotacoes_num_unico_insert_que_atualiza_o_valor_no_conflito()
    {
        // Arrange
        var (contexto, comandos) = CriarContextoSemBanco();
        using var _ = contexto;
        var repository = new IndexRateRepository(contexto);

        // Act
        await repository.UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m, 0.07m));

        // Assert
        var comando = Assert.Single(comandos.Executados);
        var sql = string.Join(' ', comando.Texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("INSERT INTO index_rate (id, index_type, reference_date, value)", sql);
        Assert.EndsWith("ON CONFLICT (index_type, reference_date) DO UPDATE SET value = EXCLUDED.value", sql);
    }

    [Fact]
    public async Task Upsert_envia_id_indexador_data_e_valor_de_cada_cotacao_como_parametros()
    {
        // Arrange
        var (contexto, comandos) = CriarContextoSemBanco();
        using var _ = contexto;
        var repository = new IndexRateRepository(contexto);
        var cdi = new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m);
        var ipca = new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m);

        // Act
        await repository.UpsertAsync(new[] { cdi, ipca });

        // Assert
        var parametros = Assert.Single(comandos.Executados).Parametros;
        Assert.Equal(new[] { cdi.Id, ipca.Id }, parametros["ids"]);
        Assert.Equal(new[] { "Cdi", "Ipca" }, parametros["index_types"]);
        Assert.Equal(new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 1) }, parametros["reference_dates"]);
        Assert.Equal(new[] { 0.05m, 0.35m }, parametros["values"]);
    }

    [Fact]
    public async Task Upsert_com_a_mesma_cotacao_repetida_no_lote_envia_so_a_ultima()
    {
        // Arrange
        // O PostgreSQL rejeita (21000) um ON CONFLICT DO UPDATE que atinge a mesma linha duas vezes.
        var (contexto, comandos) = CriarContextoSemBanco();
        using var _ = contexto;
        var repository = new IndexRateRepository(contexto);
        var data = new DateOnly(2025, 6, 2);

        // Act
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, data, 0.05m),
            new IndexRate(IndexType.Cdi, data, 0.07m),
        });

        // Assert
        var parametros = Assert.Single(comandos.Executados).Parametros;
        Assert.Equal(new[] { data }, parametros["reference_dates"]);
        Assert.Equal(new[] { 0.07m }, parametros["values"]);
    }

    [Fact]
    public async Task Upsert_com_colecao_vazia_nao_executa_nenhum_comando()
    {
        // Arrange
        var (contexto, comandos) = CriarContextoSemBanco();
        using var _ = contexto;
        var repository = new IndexRateRepository(contexto);

        // Act
        await repository.UpsertAsync(Array.Empty<IndexRate>());

        // Assert
        Assert.Empty(comandos.Executados);
    }

    [Fact]
    public async Task Listar_por_periodo_inclui_as_datas_de_inicio_e_fim()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 13), 0.06m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Cdi,
            new DateOnly(2025, 6, 2),
            new DateOnly(2025, 6, 13))).ToList();

        // Assert
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 13) },
            result.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Listar_por_periodo_deixa_de_fora_as_datas_vizinhas_ao_intervalo()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 1), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 13), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 16), 0.05m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Cdi,
            new DateOnly(2025, 6, 2),
            new DateOnly(2025, 6, 13))).ToList();

        // Assert
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 13) },
            result.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Listar_por_periodo_ignora_cotacoes_de_outro_indexador()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Ipca,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30))).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(IndexType.Ipca, result[0].IndexType);
    }

    [Fact]
    public async Task Listar_por_periodo_retorna_as_cotacoes_em_ordem_crescente_de_data()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 4), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 3), 0.05m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Cdi,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30))).ToList();

        // Assert
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 3), new DateOnly(2025, 6, 4) },
            result.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Ultima_data_sem_nenhuma_cotacoes_do_indexador_e_nula()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_data_e_a_maior_data_importada_do_indexador()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 3), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 30), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 10), 0.05m),
        });

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Equal(new DateOnly(2025, 6, 30), result);
    }

    [Fact]
    public async Task Ultima_data_de_um_indexador_sem_cotacoes_e_nula_mesmo_que_outro_tenha_dados()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[] { new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m) });

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_a_uma_data_devolve_a_mais_recente_estritamente_anterior()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 5, 1), 0.26m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.24m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 7, 1), 0.26m),
        });

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2025, 6, 1), result.ReferenceDate);
        Assert.Equal(0.24m, result.Value);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_sem_nenhuma_cotacao_antes_da_data_e_nula()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[] { new IndexRate(IndexType.Ipca, new DateOnly(2025, 7, 1), 0.26m) });

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_ignora_cotacoes_de_outro_indexador()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await SemearAsync(new[] { new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m) });

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.Null(result);
    }

    // Semeia direto no contexto InMemory, sem passar pelo upsert (que é SQL do PostgreSQL).
    private async Task SemearAsync(IEnumerable<IndexRate> cotacoes)
    {
        Context.IndexRates.AddRange(cotacoes);
        await Context.SaveChangesAsync();
    }

    private static (FixedIncomeDbContext Contexto, ComandosCapturados Comandos) CriarContextoSemBanco()
    {
        var comandos = new ComandosCapturados();
        var options = new DbContextOptionsBuilder<FixedIncomeDbContext>()
            .UseNpgsql("Host=sem-banco;Database=teste;Username=teste;Password=teste")
            .AddInterceptors(comandos, new ConexaoSemBanco())
            .Options;

        return (new FixedIncomeDbContext(options), comandos);
    }

    private sealed record ComandoExecutado(string Texto, IReadOnlyDictionary<string, object?> Parametros);

    // Registra o comando e, para escrita, impede a execução (não há banco).
    private sealed class ComandosCapturados : DbCommandInterceptor
    {
        public List<ComandoExecutado> Executados { get; } = new();

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Executados.Add(new ComandoExecutado(
                command.CommandText,
                command.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => (object?)p.Value)));

            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }

    private sealed class ConexaoSemBanco : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(InterceptionResult.Suppress());
        }
    }

    private static IReadOnlyList<IndexRate> CriarCotacoesCdi(params decimal[] valores)
    {
        // Datas consecutivas a partir de 02/06/2025. Cada chamada cria instâncias novas, como a importação faria.
        var inicio = new DateOnly(2025, 6, 2);
        return valores
            .Select((valor, i) => new IndexRate(IndexType.Cdi, inicio.AddDays(i), valor))
            .ToList();
    }
}
