namespace CustomOBD.Services.Adapters
{
    public interface IObdAdapter
    {
        event EventHandler<byte[]>? DataReceived;
        bool IsConnected { get; }
        Task<bool> ConnectAsync();
        Task SendAsync(byte[] data);
        Task DisconnectAsync();
    }
}