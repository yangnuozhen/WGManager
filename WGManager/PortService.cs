
using System.Net;
using System.Net.Sockets;

namespace PortManager
{
    public class PortService : BackgroundService, IPortService
    {
        private readonly ILogger<PortService> _logger;
        private readonly string confName;
        private readonly string configPath;
        public ushort Port
        {
            get => ReadPort();
            internal set => WritePort(value);
        }



        public PortService(ILogger<PortService> logger, IConfiguration configuration)
        {
            _logger = logger;
            confName = configuration.GetValue<string>("WireGuard:ConfigName") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(confName)) 
            {
                throw new Exception("WireGuard 配置名称未设置，无法初始化 PortService。");
            }
            configPath = $"/etc/wireguard/{confName}.conf";
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"WireGuard 配置文件未找到: {configPath}");
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ushort newPort = RefreshPort();
                    _logger.LogInformation($"[{DateTime.Now}] 已更新 WireGuard 监听端口为: {newPort}");
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
                newPort = (ushort)random.Next(40000, 65536);
            } while (newPort == Port || IsPortInUse(newPort)); // 确保新端口不同于当前端口
            Port = newPort;
            return Port;
        }

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

            // 可选：立刻让 WireGuard 应用变更（根据需要开启）
            ReloadWireGuard();
        }

        /// <summary>
        /// 重启 wg-quick 服务使改动立即生效（可选）。
        /// </summary>
        private void ReloadWireGuard()
        {
            // 严格要求 root 权限，Ubuntu 24.04 可正常运行
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
                Console.WriteLine("WireGuard 重启错误: " + error);
        }
    }
}
