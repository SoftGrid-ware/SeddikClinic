using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.BackgroundServices;

public class RecurringExpensesWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurringExpensesWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public RecurringExpensesWorker(IServiceProvider serviceProvider, ILogger<RecurringExpensesWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecurringExpensesWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var expenseService = scope.ServiceProvider.GetRequiredService<IExpenseService>();

                _logger.LogInformation("Checking for due recurring expenses...");
                var generatedCount = await expenseService.ProcessDueRecurringExpensesAsync();
                
                if (generatedCount > 0)
                {
                    _logger.LogInformation("Successfully generated {Count} recurring expenses for this cycle.", generatedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing RecurringExpensesWorker.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
