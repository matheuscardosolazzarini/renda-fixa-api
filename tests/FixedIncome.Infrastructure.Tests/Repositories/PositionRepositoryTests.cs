using FixedIncome.Domain.Entities;
using FixedIncome.Infrastructure.Repositories;
using FixedIncome.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FixedIncome.Infrastructure.Tests.Repositories;

// Cada teste grava com um contexto e lê com outro: assim a leitura passa pelo banco e não pelo
// change tracker do contexto que gravou.
[Collection("PostgreSQL")]
public class PositionRepositoryTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    // SQLSTATE 23503 = foreign_key_violation
    private const string ForeignKeyViolation = "23503";

    [Fact]
    public async Task Aporte_persistido_e_recuperado_por_id()
    {
        // Arrange
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        await new PositionRepository(Context).AddAsync(position);
        using var contextoDeLeitura = CriarContexto();

        // Act
        var result = await new PositionRepository(contextoDeLeitura).GetByIdAsync(position.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(position.Id, result!.Id);
        Assert.Equal(position.InvestedAmount, result.InvestedAmount);
    }

    [Fact]
    public async Task Listar_por_titulo_retorna_apenas_os_aportes_daquele_titulo()
    {
        // Arrange
        var assetComAporte = CriarTitulo();
        var assetSemAporte = CriarTitulo();
        Context.FixedIncomeAssets.AddRange(assetComAporte, assetSemAporte);
        await Context.SaveChangesAsync();
        var position = new Position(assetComAporte, 1_000m, new DateOnly(2025, 6, 1));
        await new PositionRepository(Context).AddAsync(position);
        using var contextoDeLeitura = CriarContexto();

        // Act
        var result = (await new PositionRepository(contextoDeLeitura).GetByAssetIdAsync(assetComAporte.Id)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(position.Id, result[0].Id);
    }

    [Fact]
    public async Task Listar_por_titulo_sem_aportes_retorna_colecao_vazia()
    {
        // Arrange
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        using var contextoDeLeitura = CriarContexto();

        // Act
        var result = await new PositionRepository(contextoDeLeitura).GetByAssetIdAsync(asset.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Listar_todos_retorna_aportes_ordenados_por_data_de_aporte_decrescente()
    {
        // Arrange
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        var repositoryDeEscrita = new PositionRepository(Context);
        var positionAntiga = new Position(asset, 1_000m, new DateOnly(2025, 1, 10));
        var positionRecente = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        var positionIntermediaria = new Position(asset, 1_000m, new DateOnly(2025, 3, 15));
        await repositoryDeEscrita.AddAsync(positionAntiga);
        await repositoryDeEscrita.AddAsync(positionRecente);
        await repositoryDeEscrita.AddAsync(positionIntermediaria);
        using var contextoDeLeitura = CriarContexto();

        // Act
        var result = (await new PositionRepository(contextoDeLeitura).GetAllAsync()).ToList();

        // Assert
        Assert.Equal(
            new[] { positionRecente.Id, positionIntermediaria.Id, positionAntiga.Id },
            result.Select(p => p.Id));
    }

    [Fact]
    public async Task Aporte_de_titulo_inexistente_e_recusado_pela_chave_estrangeira()
    {
        // Arrange
        // O título nunca é gravado: a Position só guarda o AssetId, e o banco não encontra o pai.
        var tituloNaoPersistido = CriarTitulo();
        var position = new Position(tituloNaoPersistido, 1_000m, new DateOnly(2025, 6, 1));
        var repository = new PositionRepository(Context);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(position));

        // Assert
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(ForeignKeyViolation, postgres.SqlState);
        using var contextoDeLeitura = CriarContexto();
        Assert.Empty(await new PositionRepository(contextoDeLeitura).GetAllAsync());
    }

    [Fact]
    public async Task Remover_titulo_que_tem_aportes_e_recusado_e_o_titulo_continua_gravado()
    {
        // Arrange
        // DeleteBehavior.Restrict: o banco não apaga o título em cascata nem deixa o aporte órfão.
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        await new PositionRepository(Context).AddAsync(new Position(asset, 1_000m, new DateOnly(2025, 6, 1)));

        using var contextoDeRemocao = CriarContexto();
        var repositoryDeRemocao = new FixedIncomeAssetRepository(contextoDeRemocao);
        var carregado = await repositoryDeRemocao.GetByIdAsync(asset.Id);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => repositoryDeRemocao.DeleteAsync(carregado!));

        // Assert
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(ForeignKeyViolation, postgres.SqlState);
        using var contextoDeLeitura = CriarContexto();
        Assert.NotNull(await new FixedIncomeAssetRepository(contextoDeLeitura).GetByIdAsync(asset.Id));
        Assert.Single(await new PositionRepository(contextoDeLeitura).GetByAssetIdAsync(asset.Id));
    }

    [Fact]
    public async Task Aporte_faz_ida_e_volta_pelo_banco_sem_perder_valor_e_data()
    {
        // Arrange
        // 16 dígitos inteiros e 2 decimais: o limite de numeric(18,2) da coluna invested_amount.
        const decimal valor = 9_999_999_999_999_999.99m;
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        var position = new Position(asset, valor, new DateOnly(2025, 2, 28));
        await new PositionRepository(Context).AddAsync(position);
        using var contextoDeLeitura = CriarContexto();

        // Act
        var result = await new PositionRepository(contextoDeLeitura).GetByIdAsync(position.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(valor, result!.InvestedAmount);
        Assert.Equal(new DateOnly(2025, 2, 28), result.ApplicationDate);
        Assert.Equal(asset.Id, result.AssetId);
    }

    [Fact]
    public async Task Aporte_grava_valor_e_data_no_formato_esperado_da_coluna()
    {
        // Arrange
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        var position = new Position(asset, 9_999_999_999_999_999.99m, new DateOnly(2025, 2, 28));
        await new PositionRepository(Context).AddAsync(position);
        using var contextoDeLeitura = CriarContexto();

        // Act
        var valor = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT invested_amount::text AS \"Value\" FROM position WHERE id = {position.Id}")
            .SingleAsync();
        var data = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT application_date::text AS \"Value\" FROM position WHERE id = {position.Id}")
            .SingleAsync();

        // Assert
        Assert.Equal("9999999999999999.99", valor);
        Assert.Equal("2025-02-28", data);
    }
}
