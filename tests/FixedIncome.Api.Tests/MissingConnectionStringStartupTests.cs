using System.Diagnostics;

namespace FixedIncome.Api.Tests;

// R-17: processo real, porque o que se afirma é o exit code e a saída da partida, que o
// WebApplicationFactory (mesmo processo) não consegue observar.
public class MissingConnectionStringStartupTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Sem_connection_string_a_api_falha_na_partida_com_mensagem_clara_exit_code_1_e_sem_pilha()
    {
        // Arrange
        var apiDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = apiDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(Path.Combine(apiDirectory, "FixedIncome.Api.dll"));

        // Production não carrega o appsettings.Development.json (que traz a connection string);
        // a variável é removida para o teste não herdar a da máquina de quem o roda.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        startInfo.Environment.Remove("ConnectionStrings__DefaultConnection");
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        using var process = new Process { StartInfo = startInfo };

        // Act
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(StartupTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("A API não encerrou sozinha: deveria falhar na partida sem connection string.");
        }

        var output = await stdoutTask + await stderrTask;

        // Assert
        Assert.Equal(1, process.ExitCode);
        Assert.Contains("ConnectionStrings__DefaultConnection", output);
        Assert.Contains("ConnectionStrings:DefaultConnection", output);
        Assert.DoesNotContain("   at ", output);
        Assert.DoesNotContain("Unhandled exception", output);
    }
}
