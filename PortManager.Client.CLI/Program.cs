using System.CommandLine;
using System.CommandLine.Parsing;

Option<string> hubEndpointOpt = new("--hub-url", "-h")
{
    Description = "访问 PortManager Hub 端 API 的 Endpoint。\n"
                  + "示例: https://wg-api.example.com/",
    Required = true
};

Option<int> queryCycleOpt = new("--query-cycle", "-q")
{
    Description = "查询周期，单位为秒，默认为 60 秒。",
    DefaultValueFactory = parseResult => 60
};

RootCommand rootCommand = new("WGManager Client Port Manager");
rootCommand.Options.Add(hubEndpointOpt);
rootCommand.Options.Add(queryCycleOpt);

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count == 0 && parseResult.GetValue(hubEndpointOpt) is string hubEndpoint)
{
    // Just a test
    int queryCycle = parseResult.GetValue(queryCycleOpt);
    Console.WriteLine($"Hub Endpoint: {hubEndpoint}");
    Console.WriteLine($"Query Cycle: {queryCycle} seconds");
    return 0;
}
foreach (ParseError parseError in parseResult.Errors)
{
    Console.Error.WriteLine(parseError.Message);
}
return 1;