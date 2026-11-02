using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace FixedIncome.Infrastructure.Repositories;

public class IndexRateRepository : IIndexRateRepository
{
    // Um INSERT ... ON CONFLICT por lote: a unicidade (index_type, reference_date) é resolvida no
    // banco, de forma atômica. Ler antes e gravar depois deixava uma janela em que duas instâncias
    // importando juntas violavam o índice único (23505). As colunas repetem as de
    // IndexRateConfiguration; o id novo só vale para linhas inseridas, a linha existente mantém o seu.
    private const string UpsertSql = @"
        INSERT INTO index_rate (id, index_type, reference_date, value)
        SELECT * FROM unnest(@ids, @index_types, @reference_dates, @values)
        ON CONFLICT (index_type, reference_date) DO UPDATE SET value = EXCLUDED.value";

    private readonly FixedIncomeDbContext _context;

    public IndexRateRepository(FixedIncomeDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<IndexRate>> GetByIndexTypeAndPeriodAsync(IndexType indexType, DateOnly from, DateOnly to)
    {
        return await _context.IndexRates
            .AsNoTracking()
            .Where(r => r.IndexType == indexType && r.ReferenceDate >= from && r.ReferenceDate <= to)
            .OrderBy(r => r.ReferenceDate)
            .ToListAsync();
    }

    public async Task<IndexRate?> GetLastBeforeAsync(IndexType indexType, DateOnly date)
    {
        return await _context.IndexRates
            .AsNoTracking()
            .Where(r => r.IndexType == indexType && r.ReferenceDate < date)
            .OrderByDescending(r => r.ReferenceDate)
            .FirstOrDefaultAsync();
    }

    public async Task<DateOnly?> GetLastReferenceDateAsync(IndexType indexType)
    {
        return await _context.IndexRates
            .Where(r => r.IndexType == indexType)
            .Select(r => (DateOnly?)r.ReferenceDate)
            .MaxAsync();
    }

    public async Task UpsertAsync(IEnumerable<IndexRate> rates)
    {
        // O PostgreSQL rejeita (21000) um DO UPDATE que atinge a mesma linha duas vezes no mesmo comando.
        var incoming = rates
            .GroupBy(r => (r.IndexType, r.ReferenceDate))
            .Select(g => g.Last())
            .ToList();

        if (incoming.Count == 0)
        {
            return;
        }

        await _context.Database.ExecuteSqlRawAsync(
            UpsertSql,
            new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            {
                Value = incoming.Select(r => r.Id).ToArray(),
            },
            new NpgsqlParameter("index_types", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                // A coluna guarda o enum como texto (HasConversion<string>).
                Value = incoming.Select(r => r.IndexType.ToString()).ToArray(),
            },
            new NpgsqlParameter("reference_dates", NpgsqlDbType.Array | NpgsqlDbType.Date)
            {
                Value = incoming.Select(r => r.ReferenceDate).ToArray(),
            },
            new NpgsqlParameter("values", NpgsqlDbType.Array | NpgsqlDbType.Numeric)
            {
                Value = incoming.Select(r => r.Value).ToArray(),
            });
    }
}
