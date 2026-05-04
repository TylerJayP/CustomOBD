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
    private string _connectionStatus = "";

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }
    public ObservableCollection<IDevice> DiscoveredDevices { get; set; } = new ObservableCollection<IDevice>();


    public BluetoothViewModel(IBluetoothLE ble, IAdapter adadpter)
    {
        this._ble = ble;
        this._adapter = adadpter;
    }

    public async Task ScanForDevices()
    {
        Debug.WriteLine($"BLE is on: {_ble.IsOn}, State: {_ble.State}");

        if (!_ble.IsOn)
        {
            return;
        }

        _adapter.DeviceDiscovered += (s, a) => { Debug.WriteLine("Devices Discvoed"); DiscoveredDevices.Add(a.Device); };
        await _adapter.StartScanningForDevicesAsync();
    }

    public async Task ConnectToDevice(IDevice device)
    {
        try
        {
            await _adapter.ConnectToDeviceAsync(device);
            ObdService service = new ObdService();
            await service.InitializeAsync(device);
            var vin = await service.GetVinAsync();
            ConnectionStatus = $"Connected - VIN: {vin}";
        }
        catch (DeviceConnectionException DCE)
        {
            Debug.WriteLine(DCE.Message);
        }
    }
}

