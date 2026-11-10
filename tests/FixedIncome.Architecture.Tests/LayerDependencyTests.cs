using System.Reflection;
using FixedIncome.Api.Controllers;
using FixedIncome.Domain.Entities;
using NetArchTest.Rules;

namespace FixedIncome.Architecture.Tests;

// Regras de camada do CLAUDE.md: Api -> Application -> Domain, com Infrastructure
// implementando as interfaces da Application.
public class LayerDependencyTests
{
    private const string ApplicationNamespace = "FixedIncome.Application";
    private const string InfrastructureNamespace = "FixedIncome.Infrastructure";
    private const string ApiNamespace = "FixedIncome.Api";
    private const string ControllersNamespace = "FixedIncome.Api.Controllers";

    private static readonly Assembly DomainAssembly = typeof(FixedIncomeAsset).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(FixedIncome.Application.DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(AssetsController).Assembly;

    [Fact]
    public void Domain_nao_depende_de_outras_camadas_nem_de_pacotes_de_infraestrutura()
    {
        // Arrange
        string[] forbidden =
        [
            ApplicationNamespace,
            InfrastructureNamespace,
            ApiNamespace,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions",
            "Npgsql"
        ];

        // Act
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, DescribeViolations("Domain", result));
    }

    [Fact]
    public void Domain_referencia_apenas_assemblies_do_framework()
    {
        // Arrange
        // Limitação: o compilador omite do metadata uma ProjectReference sem uso, então
        // este teste não a enxerga; a conferência por `dotnet list reference` é da revisão.
        static bool IsFramework(string name) =>
            name == "netstandard" || name == "mscorlib" || name.StartsWith("System.", StringComparison.Ordinal)
            || name == "System";

        // Act
        var outsiders = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => !IsFramework(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            outsiders.Count == 0,
            $"O Domain referencia assemblies fora do framework: {string.Join(", ", outsiders)}");
    }

    [Fact]
    public void Application_nao_depende_de_infrastructure_api_nem_de_pacotes_web_e_de_dados()
    {
        // Arrange
        string[] forbidden =
        [
            InfrastructureNamespace,
            ApiNamespace,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Npgsql"
        ];

        // Act
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, DescribeViolations("Application", result));
    }

    [Fact]
    public void Controllers_nao_dependem_de_infrastructure_nem_do_ef_core()
    {
        // Arrange
        // Mais amplo que "não acessam o DbContext": o DbContext mora na Infrastructure e o
        // controller só chama o caso de uso.
        string[] forbidden = [InfrastructureNamespace, "Microsoft.EntityFrameworkCore"];

        // Act
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace(ControllersNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, DescribeViolations("Controllers", result));
    }

    [Fact]
    public void Nenhum_tipo_fora_da_infrastructure_depende_do_dbcontext_nem_do_npgsql()
    {
        // Arrange
        // O teste dos controllers só olha o namespace deles; este cobre o resto da Api (serviços,
        // filtros, middlewares) e as demais camadas. Só a Infrastructure fala com o banco.
        string[] forbidden =
        [
            "Microsoft.EntityFrameworkCore.DbContext",
            "FixedIncome.Infrastructure.Context",
            "Npgsql"
        ];

        // Act
        var result = Types.InAssemblies([DomainAssembly, ApplicationAssembly, ApiAssembly])
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, DescribeViolations("Tipos fora da Infrastructure", result));
    }

    // NetArchTest passa em silêncio quando o filtro não casa com nada; os três testes abaixo
    // garantem que cada regra avalia um conjunto real de tipos.

    [Fact]
    public void Assembly_do_domain_tem_tipos_para_avaliar()
    {
        // Arrange
        var domainTypes = Types.InAssembly(DomainAssembly);

        // Act
        var count = domainTypes.GetTypes().Count();

        // Assert
        Assert.True(count > 0, "Nenhum tipo encontrado no assembly do Domain.");
    }

    [Fact]
    public void Assembly_da_application_tem_tipos_para_avaliar()
    {
        // Arrange
        var applicationTypes = Types.InAssembly(ApplicationAssembly);

        // Act
        var count = applicationTypes.GetTypes().Count();

        // Assert
        Assert.True(count > 0, "Nenhum tipo encontrado no assembly da Application.");
    }

    [Fact]
    public void Namespace_dos_controllers_tem_tipos_para_avaliar()
    {
        // Arrange
        var controllerTypes = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace(ControllersNamespace);

        // Act
        var count = controllerTypes.GetTypes().Count();

        // Assert
        Assert.True(count > 0, $"Nenhum tipo encontrado no namespace {ControllersNamespace}.");
    }

    private static string DescribeViolations(string layer, TestResult result)
    {
        var names = result.FailingTypeNames ?? [];
        return $"{layer} viola a regra de dependência. Tipos: {string.Join(", ", names)}";
    }
}
