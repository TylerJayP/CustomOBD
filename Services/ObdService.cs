using CustomOBD.Services.Adapters;
using System.Diagnostics;
using System.Text;

namespace CustomOBD.Services;

public class ObdService
{
    private readonly IObdAdapter _adapter;
    private StringBuilder _responseBuffer = new StringBuilder();

    public ObdService(IObdAdapter adapter)
    {
        this._adapter = adapter;
        _adapter.DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, byte[] data)
    {
        _responseBuffer.Append(Encoding.UTF8.GetString(data));
    }
    public async Task InitializeAsync()
    {
        await SendCommandAsync("ATZ");
        await Task.Delay(1000);
        await SendCommandAsync("ATE0");
        await Task.Delay(200);
        await SendCommandAsync("ATSP0");
        await Task.Delay(200);
    }

    //When a command/request is sent, the format looks like: [Mode] [PID]
    // For example: 0902
    // Mode 09 = Vehicle Info    PID 02 = VIN
    public async Task SendCommandAsync(string command)
    {
        var bytes = Encoding.UTF8.GetBytes(command + "\r");
        await _adapter.SendAsync(bytes);
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

                // Why am I looking for 49 02 and not 09 02?
                // How the header works: When I request the ECU for something it's setup like:
                /* Request Mode    Reponse Mode
                 *      09             49
                 *      01             41
                 *      03             43
                 *          
                 *      Every response is +0x40 to indicate that we are being given a response, and not a request.
                 * In lamin terms, 49 02 means: 49 = I'm responding to a Mode 09 request that is specifically for PID 02 which is the VIN
                */
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

    public async Task<int> GetRpmAsync()
    {
        _responseBuffer.Clear();
        await SendCommandAsync("010C");

        int attempts = 0;
        while (!_responseBuffer.ToString().Contains(">") && attempts < 20)
        {
            await Task.Delay(200);
            attempts++;
        }

        var bytes = ExtractDataBytes(_responseBuffer.ToString(), "410C");
        if (bytes.Length >= 2)
        {
            return ((bytes[0] * 256) + bytes[1]) / 4;
        }

        return 0;
    }

    public byte[] ExtractDataBytes(string rawResponse, string expectedHeader)
    {
        var extractedBytes = new List<byte>();
        try
        {
            var lines = rawResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var cleanedResponseLine = line.Trim().Replace(" ", ""); ;

                if (string.IsNullOrWhiteSpace(cleanedResponseLine) || cleanedResponseLine.Contains(">") || cleanedResponseLine.Contains("SEARCHING")) 
                {
                    continue;
                }

                int headerIndex = cleanedResponseLine.IndexOf(expectedHeader);
                if (headerIndex >= 0)
                {
                    string dataPortion = cleanedResponseLine.Substring(headerIndex + expectedHeader.Length);

                    for (int i = 0; i + 1 < dataPortion.Length; i += 2)
                    {
                        string hexPair = dataPortion.Substring(i, 2);
                        extractedBytes.Add(Convert.ToByte(hexPair, 16));
                    }
                }
            }
        }catch (Exception e)
        {
            Debug.WriteLine($"There was an error: {e.Message}");
        }

        return extractedBytes.ToArray();
    }
}