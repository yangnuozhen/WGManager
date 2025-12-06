namespace PortManager
{
    public interface IPortService
    {
        public ushort Port { get; }
        public ushort RefreshPort();

    }
}
