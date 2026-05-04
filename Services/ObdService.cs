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
    private StringBuilder _responseBuffer = new StringBuilder();

    public async Task InitializeAsync(IDevice device)
    {
        var service = await device.GetServiceAsync(ServiceUuid);
        _rx = await service.GetCharacteristicAsync(RxUuid);
        _tx = await service.GetCharacteristicAsync(TxUuid);

        await _tx.StartUpdatesAsync();

        _tx.ValueUpdated += (s, a) =>
        {
            _responseBuffer.Append(Encoding.UTF8.GetString(a.Characteristic.Value));
            Debug.WriteLine($"OBD Response: {_responseBuffer}");
            
        };

        // reset the EML327 -> OBD Reader
        await SendCommandAsync("ATZ");
        await Task.Delay(1000);

        // Disable extended responses
        await SendCommandAsync("ATE0");
        await Task.Delay(200);
        await SendCommandAsync("ATSP0");
        await Task.Delay(200);
    }

    public async Task SendCommandAsync(string command)
    {
        if (_rx == null) {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(command + "\r");
        await _rx.WriteAsync(bytes);
        await Task.Delay(300);
    }

    public async Task<string> GetVinAsync()
    {
        _responseBuffer.Clear();
        await SendCommandAsync("0902");

        // Wait until we see > which means EM327 is done and has go a response
        int attempts = 0;
        while(!_responseBuffer.ToString().Contains(">") && attempts < 20)
        {
            await Task.Delay(200);
            attempts++;
        }

        return ParseVin(_responseBuffer.ToString());
    }

    public string ParseVin(string rawVin)
    {
        StringBuilder normalizedVin = new StringBuilder();
        try
        {
            var lines = rawVin.Split('\n');
            foreach (var line in lines)
            {
                var trimmedVin = line.Trim();
                if (trimmedVin.Contains(">") || trimmedVin.Contains("49 02") || trimmedVin.Contains("SEARCHING") || String.IsNullOrWhiteSpace(trimmedVin) )
                {
                    continue;
                }

                if(trimmedVin.Length > 1 && trimmedVin[1] == ':')
                {
                    trimmedVin = trimmedVin.Substring(2).Trim();
                }

                var hexBytes = trimmedVin.Split(' ');
                foreach (var hex in hexBytes)
                {
                    if(hex.Length == 2)
                    {
                        char ch = (char)Convert.ToInt32(hex, 16);
                        normalizedVin.Append(ch);
                    }
                }
            }
        }catch (Exception e)
        {
            Debug.WriteLine($"Issue with Normalizing VIN: {e.Message}");
        }

        return normalizedVin.ToString();
    }

}