using Microsoft.EntityFrameworkCore;
using ProductImport.Core.Entities;
using ProductImport.Core.Interfaces;
using ProductImport.Infrastructure.Data;

namespace ProductImport.Infrastructure.Repositories;

public class ImportJobRepository : IImportJobRepository
{
    private readonly AppDbContext _context;

    public ImportJobRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> CreateImportJobAsync(ImportJob importJob, CancellationToken cancellationToken = default)
    {
        _context.ImportJobs.Add(importJob);
        await _context.SaveChangesAsync(cancellationToken);
        return importJob.Id;
    }

    public async Task UpdateImportJobAsync(ImportJob importJob, CancellationToken cancellationToken = default)
    {
        _context.ImportJobs.Update(importJob);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ImportJob?> GetImportJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ImportJobs.FindAsync(new object[] { id }, cancellationToken);
    }
}
