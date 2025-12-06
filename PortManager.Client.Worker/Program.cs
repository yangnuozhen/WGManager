using PortManager.Client.Worker;
using System.CommandLine;

Option<string> configPathOpt = new("--config-path", "-f")
{
    Description = "指定 WireGuard 隧道配置文件路径。",
    Required = true
};
Option<string> hubEndpointOpt = new("--endpoint", "-e")
{
    Description = "访问 PortManager Hub 端 API 的 Endpoint。\n"
                  + "示例: https://wg-api.example.com/",
    Required = true
};

Option<int> intervalOpt = new("--interval", "-i")
{
    Description = "查询周期，单位为秒，默认为 10 秒。",
    DefaultValueFactory = parseResult => 10
};

Option<string> interfaceOpt = new("--interface", "-n")
{
    Description = "指定 WireGuard 隧道接口名称，默认为 wg0。",
    DefaultValueFactory = parseResult => "wg0"
};

RootCommand rootCommand = new("WGManager Client Port Manager");
rootCommand.Options.Add(configPathOpt);
rootCommand.Options.Add(hubEndpointOpt);
rootCommand.Options.Add(intervalOpt);
rootCommand.Options.Add(interfaceOpt);


rootCommand.SetAction(parseResult =>
{
    string? configPath = parseResult.GetValue(configPathOpt);
    if (!File.Exists(configPath))
    {
        Console.Error.WriteLine($"配置文件不存在: {configPath}");
        return -1;
    }
    string? hubEndpoint = parseResult.GetValue(hubEndpointOpt);
    if (string.IsNullOrWhiteSpace(hubEndpoint) || !Uri.IsWellFormedUriString(hubEndpoint, UriKind.Absolute))
    {
        Console.Error.WriteLine($"无效的 Endpoint: {hubEndpoint}");
        return -1;
    }
    int interval = parseResult.GetValue(intervalOpt);
    var host = Host.CreateDefaultBuilder()
    .UseSystemd()
    .ConfigureServices(services =>
    {
        services.AddSingleton(new AppOptions
        {
            ConfigPath = configPath,
            Endpoint = hubEndpoint,
            IntervalSeconds = interval
        });

        services.AddHostedService<Worker>();
    })
    .Build();

    host.Run();
    return 0;

});

ParseResult parseResult = rootCommand.Parse(args);
return parseResult.Invoke();


