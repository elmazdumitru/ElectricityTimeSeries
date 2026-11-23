using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ElectricityTimeSeries.Api.Authorization.Enforcement;

/// <summary>Reads revocations from a JSON file and reloads it whenever it changes, standing in for a
/// policy bundle pushed to every API. If a new version can't be read, the previous one stays in force.</summary>
public sealed class FileRevocationLedger : IPolicyEnforcementPoint, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { RespectNullableAnnotations = true };

    private readonly string _path;
    private readonly PhysicalFileProvider _directory;
    private readonly IDisposable _watch;
    private readonly ILogger<FileRevocationLedger> _logger;
    private volatile RevocationLedger _ledger = RevocationLedger.Empty;

    public FileRevocationLedger(IOptions<AccessPolicyOptions> options, IHostEnvironment environment, ILogger<FileRevocationLedger> logger)
    {
        _logger = logger;
        _path = Path.GetFullPath(options.Value.LedgerPath, environment.ContentRootPath);

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        _directory = new PhysicalFileProvider(directory);

        Load();
        _watch = ChangeToken.OnChange(() => _directory.Watch(Path.GetFileName(_path)), Load);
    }

    public ValueTask<EnforcementDecision> EvaluateAsync(AccessTokenAttributes token, CancellationToken cancellationToken)
    {
        var revocation = _ledger.Revocations.FirstOrDefault(r => r.Matches(token));

        return ValueTask.FromResult(revocation is null
            ? EnforcementDecision.Permit
            : EnforcementDecision.Deny(revocation.Reason ?? "The access token has been revoked."));
    }

    public void Dispose()
    {
        _watch.Dispose();
        _directory.Dispose();
    }

    private void Load()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _ledger = File.Exists(_path)
                    ? JsonSerializer.Deserialize<RevocationLedger>(File.ReadAllText(_path), Json) ?? RevocationLedger.Empty
                    : RevocationLedger.Empty;
                _logger.LogInformation("Loaded {Count} revocation(s) from {Path}.", _ledger.Revocations.Count, _path);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(50); // The file may still be open by whoever is writing it.
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                _logger.LogError(exception, "Could not read the revocation ledger {Path}; keeping the previous version.", _path);
                return;
            }
        }
    }
}
