using CustomOBD.ViewModel;
using Plugin.BLE.Abstractions.Contracts;
using System.Diagnostics;

namespace CustomOBD
{
    public partial class MainPage : ContentPage
    {
        private BluetoothViewModel _btVM;
        public MainPage(BluetoothViewModel btVM)
        {
            this._btVM = btVM;
            BindingContext = btVM;
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Bluetooth>();
            }
        }

        private async void ScanForDevices(object sender, EventArgs e)
        {
            Debug.WriteLine("Scan for devices clicked");
            await _btVM.ScanForDevices();
        }

        private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            var device = e.CurrentSelection.FirstOrDefault() as IDevice;
            if (device != null) {
                await _btVM.ConnectToDevice(device);
            }
        }
    }
}
