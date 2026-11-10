using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Repositories;
using FixedIncome.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Tests.Repositories;

// Cada teste grava com um contexto e lê com outro: assim a leitura passa pelo banco e não pelo
// change tracker do contexto que gravou.
[Collection("PostgreSQL")]
public class FixedIncomeAssetRepositoryTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    [Fact]
    public async Task Titulo_persistido_e_recuperado_por_id()
    {
        // Arrange
        var asset = CriarTitulo();
        await new FixedIncomeAssetRepository(Context).AddAsync(asset);
        using var contextoDeLeitura = CriarContexto();
        var repositoryDeLeitura = new FixedIncomeAssetRepository(contextoDeLeitura);

        // Act
        var result = await repositoryDeLeitura.GetByIdAsync(asset.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(asset.Id, result!.Id);
        Assert.Equal(asset.Name, result.Name);
    }

    [Fact]
    public async Task Recuperar_titulo_com_id_inexistente_retorna_nulo()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepository(Context);

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Listar_todos_retorna_titulos_ordenados_por_nome()
    {
        // Arrange
        // Nomes ASCII de mesma caixa e mesmo formato: qualquer collation do banco concorda com a ordem.
        var repositoryDeEscrita = new FixedIncomeAssetRepository(Context);
        await repositoryDeEscrita.AddAsync(CriarTitulo("CDB C"));
        await repositoryDeEscrita.AddAsync(CriarTitulo("CDB A"));
        await repositoryDeEscrita.AddAsync(CriarTitulo("CDB B"));
        using var contextoDeLeitura = CriarContexto();
        var repositoryDeLeitura = new FixedIncomeAssetRepository(contextoDeLeitura);

        // Act
        var result = (await repositoryDeLeitura.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(new[] { "CDB A", "CDB B", "CDB C" }, result.Select(a => a.Name));
    }

    [Fact]
    public async Task Atualizar_titulo_persiste_as_alteracoes()
    {
        // Arrange
        var asset = CriarTitulo();
        await new FixedIncomeAssetRepository(Context).AddAsync(asset);

        using var contextoDeAtualizacao = CriarContexto();
        var repositoryDeAtualizacao = new FixedIncomeAssetRepository(contextoDeAtualizacao);
        var carregado = await contextoDeAtualizacao.FixedIncomeAssets.FindAsync(asset.Id);

        // FixedIncomeAsset não expõe mutação pública (fora do escopo desta fase);
        // o valor tracked é alterado via metadata do EF para exercitar a persistência.
        contextoDeAtualizacao.Entry(carregado!).Property(a => a.Name).CurrentValue = "CDB Banco X Atualizado";

        // Act
        await repositoryDeAtualizacao.UpdateAsync(carregado!);

        // Assert
        using var contextoDeLeitura = CriarContexto();
        var result = await new FixedIncomeAssetRepository(contextoDeLeitura).GetByIdAsync(asset.Id);
        Assert.Equal("CDB Banco X Atualizado", result!.Name);
    }

    [Fact]
    public async Task Remover_titulo_faz_com_que_deixe_de_ser_recuperado()
    {
        // Arrange
        var asset = CriarTitulo();
        await new FixedIncomeAssetRepository(Context).AddAsync(asset);

        using var contextoDeRemocao = CriarContexto();
        var repositoryDeRemocao = new FixedIncomeAssetRepository(contextoDeRemocao);
        var carregado = await repositoryDeRemocao.GetByIdAsync(asset.Id);

        // Act
        await repositoryDeRemocao.DeleteAsync(carregado!);

        // Assert
        using var contextoDeLeitura = CriarContexto();
        var result = await new FixedIncomeAssetRepository(contextoDeLeitura).GetByIdAsync(asset.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task Titulo_faz_ida_e_volta_pelo_banco_sem_perder_taxa_datas_e_enums()
    {
        // Arrange
        // 12 dígitos inteiros e 6 decimais: o limite de numeric(18,6) da coluna rate.
        const decimal taxa = 999_999_999_999.123456m;
        var asset = new FixedIncomeAsset(
            "Tesouro IPCA+ 2031",
            "Banco Y",
            AssetType.TreasuryBond,
            IndexType.Ipca,
            taxa,
            new DateOnly(2024, 2, 29),
            new DateOnly(2031, 12, 31));
        await new FixedIncomeAssetRepository(Context).AddAsync(asset);
        using var contextoDeLeitura = CriarContexto();

        // Act
        var result = await new FixedIncomeAssetRepository(contextoDeLeitura).GetByIdAsync(asset.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(taxa, result!.Rate);
        Assert.Equal(new DateOnly(2024, 2, 29), result.IssueDate);
        Assert.Equal(new DateOnly(2031, 12, 31), result.MaturityDate);
        Assert.Equal(AssetType.TreasuryBond, result.AssetType);
        Assert.Equal(IndexType.Ipca, result.IndexType);
    }

    [Fact]
    public async Task Titulo_grava_taxa_datas_e_enums_no_formato_esperado_da_coluna()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "Tesouro IPCA+ 2031",
            "Banco Y",
            AssetType.TreasuryBond,
            IndexType.Ipca,
            999_999_999_999.123456m,
            new DateOnly(2024, 2, 29),
            new DateOnly(2031, 12, 31));
        await new FixedIncomeAssetRepository(Context).AddAsync(asset);
        using var contextoDeLeitura = CriarContexto();

        // Act
        // SQL cru: o enum tem de estar gravado como texto, e não como o inteiro do enum.
        var assetType = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT asset_type AS \"Value\" FROM fixed_income_asset WHERE id = {asset.Id}")
            .SingleAsync();
        var indexType = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT index_type AS \"Value\" FROM fixed_income_asset WHERE id = {asset.Id}")
            .SingleAsync();
        var rate = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT rate::text AS \"Value\" FROM fixed_income_asset WHERE id = {asset.Id}")
            .SingleAsync();
        var issueDate = await contextoDeLeitura.Database
            .SqlQuery<string>($"SELECT issue_date::text AS \"Value\" FROM fixed_income_asset WHERE id = {asset.Id}")
            .SingleAsync();

        // Assert
        Assert.Equal("TreasuryBond", assetType);
        Assert.Equal("Ipca", indexType);
        Assert.Equal("999999999999.123456", rate);
        Assert.Equal("2024-02-29", issueDate);
    }
}
