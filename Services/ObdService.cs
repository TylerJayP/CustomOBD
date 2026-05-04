using Plugin.BLE.Abstractions.Contracts;
using System.Diagnostics;
using System.Text;

namespace CustomOBD.Services;

public class ObdService
{
    private readonly Guid ServiceUuid = Guid.Parse("0000fff0-0000-1000-8000-00805f9b34fb");
    private readonly Guid RxUuid = Guid.Parse("0000fff2-0000-1000-8000-00805f9b34fb");
    private readonly Guid TxUuid = Guid.Parse("0000fff1-0000-1000-8000-00805f9b34fb");
    
    private ICharacteristic? _rx;
    private ICharacteristic? _tx;

    private string _lastResponse = "";

    public async Task InitializeAsync(IDevice device)
    {
        var service = await device.GetServiceAsync(ServiceUuid);
        _rx = await service.GetCharacteristicAsync(RxUuid);
        _tx = await service.GetCharacteristicAsync(TxUuid);

        await _tx.StartUpdatesAsync();

        _tx.ValueUpdated += (s, a) =>
        {
            _lastResponse = Encoding.UTF8.GetString(a.Characteristic.Value);
            Debug.WriteLine($"OBD Response: {_lastResponse}");
        };

        await SendCommandAsync("ATZ");
        await Task.Delay(1000);
        await SendCommandAsync("ATE0");
        await Task.Delay(200);
        await SendCommandAsync("ATSP0");
        await Task.Delay(200);
    }

    public async Task<string> SendCommandAsync(string command)
    {
        if (_rx == null) return string.Empty;

        var response = string.Empty;
        var bytes = Encoding.UTF8.GetBytes(command + "\r");
        await _rx.WriteAsync(bytes);
        await Task.Delay(300);

        return _lastResponse;
    }

    public async Task<string> GetVinAsync()
    {
        return await SendCommandAsync("0902");
    }
}