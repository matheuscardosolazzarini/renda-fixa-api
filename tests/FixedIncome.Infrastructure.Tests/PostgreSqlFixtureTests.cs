using FixedIncome.Tests.Shared;

namespace FixedIncome.Infrastructure.Tests;

// Não sobe contêiner: só confere a tradução da falha de inicialização, que o Docker parado
// (DockerUnavailableException) e um DOCKER_HOST malformado (TypeInitializationException) disparam.
public class PostgreSqlFixtureTests
{
    [Fact]
    public void Falha_de_inicializacao_orienta_a_iniciar_o_docker_e_conferir_o_docker_host()
    {
        // Arrange
        var cause = new TypeInitializationException("Docker.DotNet.Foo", new UriFormatException("host inválido"));

        // Act
        var failure = PostgreSqlFixture.CreateStartupFailure(cause);

        // Assert
        Assert.Contains("precisam do Docker em execução", failure.Message);
        Assert.Contains("DOCKER_HOST", failure.Message);
    }

    [Fact]
    public void Falha_de_inicializacao_preserva_a_excecao_original_como_inner_exception()
    {
        // Arrange
        var cause = new InvalidOperationException("causa original");

        // Act
        var failure = PostgreSqlFixture.CreateStartupFailure(cause);

        // Assert
        Assert.Same(cause, failure.InnerException);
    }
}
