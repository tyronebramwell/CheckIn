using CheckInApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CheckInApi.Services;

public class CsvSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CsvSyncWorker> _logger;

    public CsvSyncWorker(IServiceScopeFactory scopeFactory, ILogger<CsvSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CSV Sync Background Worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var csvService = scope.ServiceProvider.GetRequiredService<CsvService>();

                // Get interval from DB
                var intervalConfig = await db.SystemConfigs.FindAsync("CSV_SYNC_INTERVAL_MINS");
                int intervalMins = 5;
                if (intervalConfig != null && int.TryParse(intervalConfig.Value, out int val))
                {
                    intervalMins = val;
                }

                _logger.LogInformation("Performing periodic CSV sync. Next sync in {Mins} minutes.", intervalMins);
                
                await csvService.ImportFromCsvAsync();
                await csvService.SyncEventsAsync();
                await csvService.SyncMembersAsync();
                await csvService.SyncAttendanceAsync(DateTime.UtcNow);

                await Task.Delay(TimeSpan.FromMinutes(intervalMins), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during background CSV sync.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Retry in 1 min on error
            }
        }

        _logger.LogInformation("CSV Sync Background Worker stopping.");
    }
}
