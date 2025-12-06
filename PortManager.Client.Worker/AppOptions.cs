namespace PortManager.Client.Worker;

public class AppOptions
{
    public string ConfigPath { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public int IntervalSeconds { get; set; }

    public string InterfaceName { get; set; } = "wg0";

}

