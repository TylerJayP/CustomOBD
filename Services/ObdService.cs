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

        await SendCommandAsync("ATZ");
        await Task.Delay(1000);
        await SendCommandAsync("ATE0");
        await Task.Delay(200);
        await SendCommandAsync("ATSP0");
        await Task.Delay(200);
    }

    public async Task SendCommandAsync(string command)
    {
        if (_rx == null) return;

        var bytes = Encoding.UTF8.GetBytes(command + "\r");
        await _rx.WriteAsync(bytes);
        await Task.Delay(300);
    }

    public async Task<string> GetVinAsync()
    {
        _responseBuffer.Clear();
        await SendCommandAsync("0902");

        int attempts = 0;
        while (!_responseBuffer.ToString().Contains(">") && attempts < 20)
        {
            await Task.Delay(200);
            attempts++;
        }

        return ParseVin(_responseBuffer.ToString());
    }

    public string ParseVin(string rawVin)
    {
        Debug.WriteLine($"Raw VIN input: '{rawVin}'");
        StringBuilder normalizedVin = new StringBuilder();
        try
        {
            var lines = rawVin.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedVin = line.Trim();
                Debug.WriteLine($"Raw line: '{trimmedVin}'");

                // Skip empty, prompts, and noise
                if (string.IsNullOrWhiteSpace(trimmedVin)) continue;
                if (trimmedVin.Contains(">")) continue;
                if (trimmedVin.Contains("SEARCHING")) continue;
                if (trimmedVin == "014") continue;

                // Strip frame numbers like "0:", "1:", "2:"
                if (trimmedVin.Length > 1 && trimmedVin[1] == ':')
                    trimmedVin = trimmedVin.Substring(2).Trim();

                // Strip 49 02 01 header
                if (trimmedVin.Contains("49 02"))
                    trimmedVin = trimmedVin.Substring(trimmedVin.IndexOf("49 02") + 8).Trim();

                Debug.WriteLine($"Line after processing: '{trimmedVin}'");

                var hexBytes = trimmedVin.Split(' ');
                foreach (var hex in hexBytes)
                {
                    Debug.WriteLine($"Processing hex: '{hex}'");
                    if (hex.Length == 2)
                    {
                        char ch = (char)Convert.ToInt32(hex, 16);
                        normalizedVin.Append(ch);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Issue with Normalizing VIN: {e.Message}");
        }

        return normalizedVin.ToString();
    }

    public async Task<string> GetRpmAsync()
    {
        _responseBuffer.Clear();
        await SendCommandAsync("010C");

        int attempts = 0;
        while (!_responseBuffer.ToString().Contains(">") && attempts < 20)
        {
            await Task.Delay(200);
            attempts++;
        }

        return ParseRPM(_responseBuffer.ToString());
    }

    public string ParseRPM(string rawRPM)
    {
        StringBuilder resultingRPM = new StringBuilder();
        try
        {
            var lines = rawRPM.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Check both formats: "41 0C" with spaces or "410C" without
                if (trimmed.Contains("41 0C") || trimmed.Contains("410C"))
                {
                    // Strip spaces so we can work with consistent format
                    var clean = trimmed.Replace(" ", "");

                    // Find where 410C is and grab the 4 hex chars after it
                    int index = clean.IndexOf("410C");
                    if (index >= 0 && clean.Length >= index + 8)
                    {
                        string aHex = clean.Substring(index + 4, 2);
                        string bHex = clean.Substring(index + 6, 2);

                        // Now you need to:
                        // 1. Convert aHex and bHex to integers (Convert.ToInt32 with base 16)
                        // 2. Apply formula: ((A * 256) + B) / 4
                        // 3. Return as string
                        int a = Convert.ToInt32(aHex, 16);
                        int b = Convert.ToInt32(bHex, 16);
                        int rpm = ((a * 256) + b) / 4;
                        resultingRPM.Append(rpm);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Issue parsing RPM: {e.Message}");
        }


        return resultingRPM.ToString();
    }
}