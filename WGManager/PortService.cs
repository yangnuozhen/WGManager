
using System.Net;
using System.Net.Sockets;

namespace PortManager
{
    public class PortService : BackgroundService, IPortService
    {
        private readonly ILogger<PortService> _logger;
        // 配置名称 & 路径
        private readonly string confName;
        private readonly string configPath;
        // 端口范围
        private readonly int minPort;
        private readonly int maxPort;
        public ushort Port
        {
            get => ReadPort();
            internal set => WritePort(value);
        }



        public PortService(ILogger<PortService> logger, IConfiguration configuration)
        {
            _logger = logger;
            confName = configuration["WireGuard:ConfigName"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(confName))
            {
                throw new Exception("WireGuard 配置名称未设置，无法初始化 PortService。");
            }
            // TODO: 支持自定义路径
            configPath = $"/etc/wireguard/{confName}.conf";
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"WireGuard 配置文件未找到: {configPath}");
            }
            string? minPortStr = configuration["WireGuard:MinPort"];
            string? maxPortStr = configuration["WireGuard:MaxPort"];
            if (string.IsNullOrWhiteSpace(minPortStr) || string.IsNullOrWhiteSpace(maxPortStr))
            {
                _logger.LogWarning("未配置 MinPort 或 MaxPort，使用默认范围 40000-50000。");
                minPort = 40000;
                maxPort = 50000;
            }
            else
            {
                if (!int.TryParse(minPortStr, out minPort) || !int.TryParse(maxPortStr, out maxPort)
                    || minPort < 1 || maxPort > 65535 || minPort >= maxPort)
                {
                    _logger.LogWarning("MinPort 和 MaxPort 配置无效，必须为 1-65535 之间的数字，且 MinPort 小于 MaxPort。\n" +
                        "将使用默认范围 40000-50000。");
                    minPort = 40000;
                    maxPort = 50000;
                }
            }
            _logger.LogInformation($"使用的端口范围: {minPort}-{maxPort}, 请记得在防火墙 & 云服务提供商安全组中放行UDP端口范围。");
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ushort newPort = RefreshPort();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "更新 WireGuard 监听端口时发生错误。");
                }
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            
        }

        public ushort RefreshPort()
        {
            var random = new Random();
            ushort newPort;
            do
            {
                newPort = (ushort)random.Next(minPort, maxPort);
            } while (newPort == Port || IsPortInUse(newPort)); // 确保新端口不同于当前端口
            Port = newPort;
            return Port;
        }

        // ChatGPT 想到的，太天才
        public static bool IsPortInUse(ushort port)
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
                return false;      // 启动成功 → 端口未被占用
            }
            catch (SocketException)
            {
                return true;       // 启动失败 → 端口被占用
            }
        }
        private ushort ReadPort()
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"配置文件不存在: {configPath}");

            var lines = File.ReadAllLines(configPath);

            var portLine = lines.FirstOrDefault(l =>
                l.TrimStart().StartsWith("ListenPort", StringComparison.OrdinalIgnoreCase));

            if (portLine == null)
                throw new Exception("配置文件中未找到 ListenPort 字段。");

            var parts = portLine.Split('=', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new Exception("ListenPort 行格式错误。");

            if (ushort.TryParse(parts[1].Trim(), out ushort port))
                return port;

            throw new Exception("ListenPort 不是有效的数字。");
        }

        private void WritePort(ushort newPort)
        {
            if (newPort < 1 || newPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(newPort), "端口必须在 1–65535 之间。");
            if (IsPortInUse(newPort))
                throw new InvalidOperationException($"端口 {newPort} 已被占用，无法设置。");

            var lines = File.ReadAllLines(configPath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("ListenPort", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"ListenPort = {newPort}";
                    break;
                }
            }

            // 回写文件
            File.WriteAllLines(configPath, lines);

                    
            _logger.LogInformation($"[{DateTime.Now}]WireGuard 监听端口已更新为 {newPort}。即将重启服务...");

            // 重载 wg-quick systemctl 服务
            ReloadWireGuard();
        }

        /// <summary>
        /// 重启 wg-quick 服务使改动立即生效。
        /// </summary>
        private void ReloadWireGuard()
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/sudo",
                    Arguments = $"systemctl restart wg-quick@{confName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
                _logger.LogError("WireGuard 重启错误: " + error);
            else
                _logger.LogInformation($"[{DateTime.Now}]WireGuard 重启成功: " + output);
        }
    }
}
