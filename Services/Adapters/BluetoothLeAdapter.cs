using CustomOBD.Services;
using Plugin.BLE.Abstractions.Contracts;
using System.Diagnostics;

namespace CustomOBD.Services.Adapters;

public class BluetoothLeAdapter : IObdAdapter
{
    private readonly IDevice _device;
    private ICharacteristic? _writeChar;
    private ICharacteristic? _notifyChar;

    public event EventHandler<byte[]>? DataReceived;
    public bool IsConnected => _writeChar != null && _notifyChar != null;

    public BluetoothLeAdapter(IDevice device)
    {
        _device = device;
    }

    public async Task<bool> ConnectAsync()
    {
        var services = await _device.GetServicesAsync();
        foreach (var service in services)
        {
            Debug.WriteLine($"Checking service: {service.Id}");
            var characteristics = await service.GetCharacteristicsAsync();

            ICharacteristic? writeChar = null;
            ICharacteristic? notifyChar = null;

            foreach (var c in characteristics)
            {
                Debug.WriteLine($"  Characteristic {c.Id}: CanWrite={c.CanWrite}, CanUpdate={c.CanUpdate}");
                if (c.CanWrite) writeChar ??= c;
                if (c.CanUpdate) notifyChar ??= c;
            }

            // Only break if BOTH found IN THE SAME SERVICE
            if (writeChar != null && notifyChar != null)
            {
                _writeChar = writeChar;
                _notifyChar = notifyChar;
                Debug.WriteLine($"Using service: {service.Id}");
                Debug.WriteLine($"Write characteristic: {writeChar.Id}");
                Debug.WriteLine($"Notify characteristic: {notifyChar.Id}");
                break;
            }
        }

        if (!IsConnected) return false;

        await _notifyChar!.StartUpdatesAsync();
        _notifyChar.ValueUpdated += (s, a) =>
        {
            DataReceived?.Invoke(this, a.Characteristic.Value);
        };

        return true;
    }

    public async Task SendAsync(byte[] data)
    {
        if (_writeChar != null)
            await _writeChar.WriteAsync(data);
    }

    public Task DisconnectAsync()
    {
        // Plugin.BLE handles disconnect at the adapter level
        return Task.CompletedTask;
    }
}

