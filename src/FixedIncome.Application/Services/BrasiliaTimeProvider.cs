namespace FixedIncome.Application.Services;

// Relógio de produção: o instante é o do sistema, mas o fuso local é o de Brasília, para
// GetLocalNow() devolver a data de "hoje" do investidor e não a UTC (R-01).
public sealed class BrasiliaTimeProvider : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone { get; } = FindBrasiliaTimeZone();

    private static TimeZoneInfo FindBrasiliaTimeZone()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Máquina sem tzdata: o Brasil não tem horário de verão desde 2019, então UTC-3 fixo
        // dá a mesma data de hoje.
        return TimeZoneInfo.CreateCustomTimeZone(
            "America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }
}
