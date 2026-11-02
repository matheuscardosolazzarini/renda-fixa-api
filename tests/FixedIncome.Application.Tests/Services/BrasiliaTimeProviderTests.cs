using FixedIncome.Application.Services;

namespace FixedIncome.Application.Tests.Services;

public class BrasiliaTimeProviderTests
{
    [Fact]
    public void Fuso_local_e_o_de_brasilia_tres_horas_atras_de_utc()
    {
        // Arrange
        var provider = new BrasiliaTimeProvider();

        // Act
        var offset = provider.LocalTimeZone.GetUtcOffset(new DateTimeOffset(2026, 10, 31, 1, 37, 0, TimeSpan.Zero));

        // Assert
        Assert.Equal(TimeSpan.FromHours(-3), offset);
    }
}
