using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CustomOBD.ViewModel;
public class BluetoothViewModel : ObservableObject
{
    readonly IBluetoothLE _ble;
    readonly IAdapter _adapter;
    public ObservableCollection<IDevice> DiscoveredDevices = new ObservableCollection<IDevice>();

    public BluetoothViewModel(IBluetoothLE ble, IAdapter adadpter)
    {
        this._ble = ble;
        this._adapter = adadpter;
    }

    public async Task ScanForDevices()
    {
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
        }
        catch (DeviceConnectionException DCE)
        {
            Debug.WriteLine(DCE.Message);
        }
    }
}

