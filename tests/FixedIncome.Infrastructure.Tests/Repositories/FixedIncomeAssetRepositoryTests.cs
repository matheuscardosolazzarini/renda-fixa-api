using FixedIncome.Infrastructure.Repositories;

namespace FixedIncome.Infrastructure.Tests.Repositories;

public class FixedIncomeAssetRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Titulo_persistido_e_recuperado_por_id()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepository(Context);
        var asset = CriarTitulo();

        // Act
        await repository.AddAsync(asset);
        var result = await repository.GetByIdAsync(asset.Id);

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
        var repository = new FixedIncomeAssetRepository(Context);
        var assetC = CriarTitulo("CDB C");
        var assetA = CriarTitulo("CDB A");
        var assetB = CriarTitulo("CDB B");
        await repository.AddAsync(assetC);
        await repository.AddAsync(assetA);
        await repository.AddAsync(assetB);

        // Act
        var result = (await repository.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(new[] { "CDB A", "CDB B", "CDB C" }, result.Select(a => a.Name));
    }

    [Fact]
    public async Task Atualizar_titulo_persiste_as_alteracoes()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepository(Context);
        var asset = CriarTitulo();
        await repository.AddAsync(asset);

        // FixedIncomeAsset não expõe mutação pública (fora do escopo desta fase);
        // o valor tracked é alterado via metadata do EF para exercitar a persistência.
        Context.Entry(asset).Property(a => a.Name).CurrentValue = "CDB Banco X Atualizado";

        // Act
        await repository.UpdateAsync(asset);
        var result = await repository.GetByIdAsync(asset.Id);

        // Assert
        Assert.Equal("CDB Banco X Atualizado", result!.Name);
    }

    [Fact]
    public async Task Remover_titulo_faz_com_que_deixe_de_ser_recuperado()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepository(Context);
        var asset = CriarTitulo();
        await repository.AddAsync(asset);

        // Act
        await repository.DeleteAsync(asset);
        var result = await repository.GetByIdAsync(asset.Id);

        // Assert
        Assert.Null(result);
    }
}
