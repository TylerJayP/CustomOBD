using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CustomOBD.Services;

namespace CustomOBD.ViewModel;

public class BluetoothViewModel : ObservableObject
{
    readonly IBluetoothLE _ble;
    readonly IAdapter _adapter;
    private ObdService? _obdService;
    private bool _isPolling = false;

    private string _connectionStatus = "";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    private double _currentRpm;
    public double CurrentRpm
    {
        get => _currentRpm;
        set => SetProperty(ref _currentRpm, value);
    }

    public ObservableCollection<IDevice> DiscoveredDevices { get; set; } = new ObservableCollection<IDevice>();

    public BluetoothViewModel(IBluetoothLE ble, IAdapter adapter)
    {
        this._ble = ble;
        this._adapter = adapter;
    }

    public async Task ScanForDevices()
    {
        Debug.WriteLine($"BLE is on: {_ble.IsOn}, State: {_ble.State}");

        if (!_ble.IsOn) return;

        _adapter.DeviceDiscovered += (s, a) => {
            Debug.WriteLine("Device Discovered");
            DiscoveredDevices.Add(a.Device);
        };
        await _adapter.StartScanningForDevicesAsync();
    }

    public async Task ConnectToDevice(IDevice device)
    {
        try
        {
            await _adapter.ConnectToDeviceAsync(device);
            _obdService = new ObdService();
            await _obdService.InitializeAsync(device);

            var vin = await _obdService.GetVinAsync();
            ConnectionStatus = $"Connected - VIN: {vin}";

            // Start polling RPM in background
            _ = StartPolling();
        }
        catch (DeviceConnectionException DCE)
        {
            Debug.WriteLine(DCE.Message);
            ConnectionStatus = $"Connection failed: {DCE.Message}";
        }
    }

    private async Task StartPolling()
    {
        if (_obdService == null) return;

        _isPolling = true;
        while (_isPolling)
        {
            try
            {
                var rpmString = await _obdService.GetRpmAsync();
                if (double.TryParse(rpmString, out double rpm))
                {
                    CurrentRpm = rpm;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Polling error: {e.Message}");
            }
            await Task.Delay(150);
        }
    }

    public void StopPolling()
    {
        _isPolling = false;
    }
}