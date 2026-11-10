using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Infrastructure.Repositories;
using FixedIncome.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FixedIncome.Infrastructure.Tests.Repositories;

// O UpsertAsync é SQL do PostgreSQL (INSERT ... ON CONFLICT): estes testes o exercitam no banco
// real, que é onde o efeito do ON CONFLICT e do índice único (index_type, reference_date) existe.
// Cada teste grava com um contexto e lê com outro, para a leitura passar pelo banco e não pelo
// change tracker.
[Collection("PostgreSQL")]
public class IndexRateRepositoryTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    // SQLSTATE 23505 = unique_violation
    private const string UniqueViolation = "23505";

    [Fact]
    public async Task Upsert_grava_todas_as_cotacoes_do_lote()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);

        // Act
        await repository.UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m, 0.07m));

        // Assert
        var gravadas = await LerCdiAsync();
        Assert.Equal(new[] { 0.05m, 0.06m, 0.07m }, gravadas.Select(r => r.Value));
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 3), new DateOnly(2025, 6, 4) },
            gravadas.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Upsert_preserva_indexador_data_e_valor_de_cada_cotacao()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        var cdi = new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m);
        var ipca = new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m);

        // Act
        await repository.UpsertAsync(new[] { cdi, ipca });

        // Assert
        using var contextoDeLeitura = CriarContexto();
        var cotacoes = await contextoDeLeitura.IndexRates.AsNoTracking().ToListAsync();
        Assert.Equal(2, cotacoes.Count);
        var cdiGravado = Assert.Single(cotacoes, r => r.IndexType == IndexType.Cdi);
        Assert.Equal(new DateOnly(2025, 6, 2), cdiGravado.ReferenceDate);
        Assert.Equal(0.05m, cdiGravado.Value);
        var ipcaGravado = Assert.Single(cotacoes, r => r.IndexType == IndexType.Ipca);
        Assert.Equal(new DateOnly(2025, 6, 1), ipcaGravado.ReferenceDate);
        Assert.Equal(0.35m, ipcaGravado.Value);
    }

    [Fact]
    public async Task Upsert_com_a_mesma_cotacao_repetida_no_lote_grava_so_a_ultima_sem_erro()
    {
        // Arrange
        // O PostgreSQL rejeita (21000) um ON CONFLICT DO UPDATE que atinge a mesma linha duas vezes.
        var repository = new IndexRateRepository(Context);
        var data = new DateOnly(2025, 6, 2);

        // Act
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, data, 0.05m),
            new IndexRate(IndexType.Cdi, data, 0.07m),
        });

        // Assert
        var gravadas = await LerCdiAsync();
        var unica = Assert.Single(gravadas);
        Assert.Equal(data, unica.ReferenceDate);
        Assert.Equal(0.07m, unica.Value);
    }

    [Fact]
    public async Task Upsert_com_colecao_vazia_nao_altera_a_tabela()
    {
        // Arrange
        await new IndexRateRepository(Context).UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m));
        var antes = await LerCdiAsync();
        var repository = new IndexRateRepository(CriarContexto());

        // Act
        await repository.UpsertAsync(Array.Empty<IndexRate>());

        // Assert
        var depois = await LerCdiAsync();
        Assert.Equal(antes.Select(r => (r.Id, r.ReferenceDate, r.Value)), depois.Select(r => (r.Id, r.ReferenceDate, r.Value)));
    }

    [Fact]
    public async Task Upsert_de_data_existente_atualiza_o_valor_e_mantem_o_id_da_linha()
    {
        // Arrange
        var data = new DateOnly(2025, 6, 2);
        await new IndexRateRepository(Context).UpsertAsync(new[] { new IndexRate(IndexType.Cdi, data, 0.05m) });
        var original = Assert.Single(await LerCdiAsync());

        // Act
        // A cotação nova nasce com outro Id; no conflito, a linha existente mantém o seu.
        var repository = new IndexRateRepository(CriarContexto());
        await repository.UpsertAsync(new[] { new IndexRate(IndexType.Cdi, data, 0.09m) });

        // Assert
        var atual = Assert.Single(await LerCdiAsync());
        Assert.Equal(original.Id, atual.Id);
        Assert.Equal(0.09m, atual.Value);
    }

    [Fact]
    public async Task Upsert_repetido_com_o_mesmo_lote_nao_duplica_registros()
    {
        // Arrange
        // Cada chamada cria instâncias novas, como a importação faria.
        await new IndexRateRepository(Context).UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m, 0.07m));

        // Act
        var repository = new IndexRateRepository(CriarContexto());
        await repository.UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m, 0.07m));

        // Assert
        var gravadas = await LerCdiAsync();
        Assert.Equal(3, gravadas.Count);
        Assert.Equal(3, gravadas.Select(r => r.ReferenceDate).Distinct().Count());
    }

    [Fact]
    public async Task Upserts_concorrentes_com_as_mesmas_datas_deixam_uma_linha_por_data_sem_violar_o_indice_unico()
    {
        // Arrange
        // Duas instâncias lendo a mesma série do SGS: mesmas chaves, na mesma ordem, em conexões
        // distintas e liberadas ao mesmo tempo. Com leitura antes de gravação, as duas inseririam a
        // mesma data e uma violaria o índice único (23505).
        const int quantidade = 300;
        var valoresA = Enumerable.Range(0, quantidade).Select(i => 0.01m + i / 1_000_000m).ToArray();
        var valoresB = Enumerable.Range(0, quantidade).Select(i => 0.02m + i / 1_000_000m).ToArray();
        var comeco = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tarefaA = UpsertQuandoLiberadoAsync(comeco.Task, CriarContexto(), CriarCotacoesCdi(valoresA));
        var tarefaB = UpsertQuandoLiberadoAsync(comeco.Task, CriarContexto(), CriarCotacoesCdi(valoresB));

        // Act
        comeco.SetResult();
        await Task.WhenAll(tarefaA, tarefaB);

        // Assert
        var gravadas = await LerCdiAsync();
        Assert.Equal(quantidade, gravadas.Count);
        Assert.Equal(quantidade, gravadas.Select(r => r.ReferenceDate).Distinct().Count());
        // Qual lote vence a corrida não é determinístico, mas cada data guarda o valor de um dos dois.
        for (var i = 0; i < quantidade; i++)
        {
            Assert.Contains(gravadas[i].Value, new[] { valoresA[i], valoresB[i] });
        }
    }

    [Fact]
    public async Task Indice_unico_recusa_duas_cotacoes_do_mesmo_indexador_na_mesma_data()
    {
        // Arrange
        var data = new DateOnly(2025, 6, 2);
        Context.IndexRates.AddRange(
            new IndexRate(IndexType.Cdi, data, 0.05m),
            new IndexRate(IndexType.Cdi, data, 0.06m));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync());

        // Assert
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(UniqueViolation, postgres.SqlState);
        Assert.Empty(await LerCdiAsync());
    }

    [Fact]
    public async Task Cotacao_faz_ida_e_volta_pelo_banco_sem_perder_valor_e_data()
    {
        // Arrange
        // 6 dígitos inteiros e 6 decimais: o limite de numeric(12,6) da coluna value, o mais apertado.
        const decimal valor = 999_999.654321m;
        var data = new DateOnly(2024, 2, 29);
        var cotacao = new IndexRate(IndexType.Cdi, data, valor);
        Context.IndexRates.Add(cotacao);
        await Context.SaveChangesAsync();

        // Act
        var lida = Assert.Single(await LerCdiAsync());

        // Assert
        Assert.Equal(cotacao.Id, lida.Id);
        Assert.Equal(valor, lida.Value);
        Assert.Equal(data, lida.ReferenceDate);
        Assert.Equal(IndexType.Cdi, lida.IndexType);
    }

    [Fact]
    public async Task Cotacao_grava_valor_data_e_indexador_no_formato_esperado_da_coluna()
    {
        // Arrange
        var cotacao = new IndexRate(IndexType.Ipca, new DateOnly(2024, 2, 1), -0.123456m);
        Context.IndexRates.Add(cotacao);
        await Context.SaveChangesAsync();
        using var contextoDeLeitura = CriarContexto();

        // Act
        // SQL cru: o enum tem de estar gravado como texto, e não como o inteiro do enum.
        var indexType = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT index_type AS \"Value\" FROM index_rate WHERE id = {cotacao.Id}")
            .SingleAsync();
        var valor = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT value::text AS \"Value\" FROM index_rate WHERE id = {cotacao.Id}")
            .SingleAsync();
        var data = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT reference_date::text AS \"Value\" FROM index_rate WHERE id = {cotacao.Id}")
            .SingleAsync();

        // Assert
        Assert.Equal("Ipca", indexType);
        Assert.Equal("-0.123456", valor);
        Assert.Equal("2024-02-01", data);
    }

    [Fact]
    public async Task Listar_por_periodo_inclui_as_datas_de_inicio_e_fim()
    {
        // Arrange
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 13), 0.06m),
        });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

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
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 1), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 13), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 16), 0.05m),
        });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

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
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m),
        });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

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
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 4), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 3), 0.05m),
        });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

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
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_data_e_a_maior_data_importada_do_indexador()
    {
        // Arrange
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 3), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 30), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 10), 0.05m),
        });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Equal(new DateOnly(2025, 6, 30), result);
    }

    [Fact]
    public async Task Ultima_data_de_um_indexador_sem_cotacoes_e_nula_mesmo_que_outro_tenha_dados()
    {
        // Arrange
        await SemearAsync(new[] { new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m) });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_a_uma_data_devolve_a_mais_recente_estritamente_anterior()
    {
        // Arrange
        await SemearAsync(new[]
        {
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 5, 1), 0.26m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.24m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 7, 1), 0.26m),
        });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

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
        await SemearAsync(new[] { new IndexRate(IndexType.Ipca, new DateOnly(2025, 7, 1), 0.26m) });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_ignora_cotacoes_de_outro_indexador()
    {
        // Arrange
        await SemearAsync(new[] { new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m) });
        using var contextoDeLeitura = CriarContexto();
        var repository = new IndexRateRepository(contextoDeLeitura);

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.Null(result);
    }

    // Semeia pelo EF, sem passar pelo upsert, para as consultas não dependerem do SQL do upsert.
    private async Task SemearAsync(IEnumerable<IndexRate> cotacoes)
    {
        Context.IndexRates.AddRange(cotacoes);
        await Context.SaveChangesAsync();
    }

    // Lê as cotações de CDI com um contexto novo, direto do banco, em ordem de data.
    private async Task<List<IndexRate>> LerCdiAsync()
    {
        using var contexto = CriarContexto();
        return await contexto.IndexRates
            .AsNoTracking()
            .Where(r => r.IndexType == IndexType.Cdi)
            .OrderBy(r => r.ReferenceDate)
            .ToListAsync();
    }

    private static async Task UpsertQuandoLiberadoAsync(
        Task liberacao,
        FixedIncomeDbContext contexto,
        IReadOnlyList<IndexRate> cotacoes)
    {
        await liberacao;
        await new IndexRateRepository(contexto).UpsertAsync(cotacoes);
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
