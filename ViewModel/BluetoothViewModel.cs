using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CustomOBD.Services;
using CustomOBD.Services.Adapters;

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

    private double _currentStft;
    public double CurrentStft
    {
        get => _currentStft;
        set => SetProperty(ref _currentStft, value);
    }

    private double _currentLtft;
    public double CurrentLtft
    {
        get => _currentLtft;
        set => SetProperty(ref _currentLtft, value);
    }

    private double _currentBoost;
    public double CurrentBoost
    {
        get => _currentBoost;
        set => SetProperty(ref _currentBoost, value);
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

            var bleAdapter = new BluetoothLeAdapter(device);
            if(!await bleAdapter.ConnectAsync())
            {
                ConnectionStatus = "Could not find OBD service on the device you selected...";
                return;
            }

            _obdService = new ObdService(bleAdapter);
            await _obdService.InitializeAsync();

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
        int slowCounter = 0;

        while (_isPolling)
        {
            try
            {
                // Fast tier - every cycle
                CurrentRpm = await _obdService.GetRpmAsync();
                CurrentBoost = await _obdService.GetBoostAsync();

                // Slow tier - every 10th cycle (~once per second)
                slowCounter++;
                if (slowCounter >= 10)
                {
                    CurrentStft = await _obdService.GetShortTermFuelTrimBank1();
                    CurrentLtft = await _obdService.GetLongTermFuelTrimBank1();
                    slowCounter = 0;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Polling error: {e.Message}");
            }

            await Task.Delay(50);
        }
    }

    public void StopPolling()
    {
        _isPolling = false;
    }
}