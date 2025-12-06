using System.Text.RegularExpressions;

namespace PortManager.Client.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AppOptions _opts;
    private readonly HttpClient _http = new();
    private string apiUrl = string.Empty;

    public Worker(ILogger<Worker> logger, AppOptions opts)
    {
        _logger = logger;
        _opts = opts;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("端口同步 Worker 正启动。");
        _logger.LogInformation("ConfigPath = {0}", _opts.ConfigPath);
        _logger.LogInformation("Endpoint   = {0}", _opts.Endpoint);
        _logger.LogInformation("Interval   = {0}s", _opts.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                apiUrl = _opts.Endpoint.TrimEnd('/') + "/api/port";
                var portStr = await _http.GetStringAsync(apiUrl, stoppingToken);
                if (!ushort.TryParse(portStr.Trim(), out var newPort))
                {
                    _logger.LogError("从服务器获取到无效的端口: {0}", portStr);
                }
                else
                {
                    UpdateConfigFile(newPort);
                    ApplyWireGuardConfig();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_opts.IntervalSeconds), stoppingToken);
        }
    }

    private void UpdateConfigFile(ushort port)
    {
        string conf = File.ReadAllText(_opts.ConfigPath);

        var regex = new Regex(@"Endpoint\s*=\s*(.+?):(\d+)");
        var updated = regex.Replace(conf, m =>
        {
            string host = m.Groups[1].Value;
            return $"Endpoint = {host}:{port}";
        });

        File.WriteAllText(_opts.ConfigPath, updated);

        _logger.LogInformation("已成功更新新的 Endpoint 端口: {0}", port);
    }

    private void ApplyWireGuardConfig()
    {
        _logger.LogInformation("即将重载 WireGuard 配置...");

        LinuxShell.Run("bash", "-c \"wg syncconf wgx <(wg-quick strip wgx)\"");

        _logger.LogInformation("已重载 WireGuard 配置。");
    }
}
