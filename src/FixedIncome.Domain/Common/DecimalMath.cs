namespace FixedIncome.Domain.Common;

// Potência com expoente fracionário em decimal. Math.Pow opera em double (15 a 17 dígitos
// significativos, e a conversão de volta para decimal ainda corta para 15); como os casos de
// referência trazem fatores com 16 casas e o valor bruto multiplica o principal, a potência é
// calculada em decimal por exp(y * ln x), com cerca de 26 dígitos corretos, sem pacote
// externo. Expoente inteiro usa multiplicação exata, para que 1,10 ^ 1 e 1,10 ^ 2 sejam
// exatamente 1,10 e 1,21.
public static class DecimalMath
{
    private const decimal Ln2 = 0.6931471805599453094172321215m;
    private const decimal E = 2.7182818284590452353602874714m;

    public static decimal Pow(decimal x, decimal exponent)
    {
        if (x <= 0)
        {
            throw new DomainException("A base da potência deve ser positiva.");
        }

        if (exponent == 0 || x == 1)
        {
            return 1m;
        }

        try
        {
            if (exponent == decimal.Truncate(exponent) && Math.Abs(exponent) <= int.MaxValue)
            {
                return IntegerPow(x, (int)exponent);
            }

            return Exp(exponent * Ln(x));
        }
        catch (Exception ex) when (ex is OverflowException or DivideByZeroException)
        {
            throw new DomainException("O fator de crescimento está fora do intervalo representável.");
        }
    }

    private static decimal IntegerPow(decimal x, int exponent)
    {
        var n = Math.Abs((long)exponent);
        var result = 1m;
        var square = x;

        while (n > 0)
        {
            if ((n & 1) == 1)
            {
                result *= square;
            }

            n >>= 1;
            if (n > 0)
            {
                square *= square;
            }
        }

        return exponent < 0 ? 1m / result : result;
    }

    // ln x = 2 * atanh((x - 1) / (x + 1)), com x reduzido para [0,5; 2] por potências de 2.
    private static decimal Ln(decimal x)
    {
        var k = 0;
        while (x > 2m)
        {
            x /= 2m;
            k++;
        }

        while (x < 0.5m)
        {
            x *= 2m;
            k--;
        }

        var z = (x - 1m) / (x + 1m);
        var z2 = z * z;
        var term = z;
        var sum = 0m;

        for (var n = 1; term != 0m; n += 2)
        {
            sum += term / n;
            term *= z2;
        }

        return 2m * sum + k * Ln2;
    }

    // exp t = e^n * exp(f), com n inteiro e f em [-0,5; 0,5] para a série de Taylor convergir rápido.
    private static decimal Exp(decimal t)
    {
        var n = (int)Math.Round(t);
        var f = t - n;

        var term = 1m;
        var sum = 1m;

        for (var i = 1; term != 0m; i++)
        {
            term = term * f / i;
            sum += term;
        }

        return sum * IntegerPow(E, n);
    }
}
