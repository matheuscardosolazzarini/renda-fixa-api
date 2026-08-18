namespace FixedIncome.Domain.ValueObjects;

// O domínio não conhece a origem desses valores — quem os fornece é responsabilidade
// de camadas externas (configuração, integração de mercado etc.).
public record IndexRates(decimal Cdi, decimal Ipca);
