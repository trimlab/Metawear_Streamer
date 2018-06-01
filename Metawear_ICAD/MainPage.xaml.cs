using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using MbientLab.MetaWear.Win10;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using Windows.ApplicationModel;
using MbientLab.MetaWear.Peripheral;
using MbientLab.MetaWear.Sensor;
using MbientLab.MetaWear.Data;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Networking.Connectivity;

namespace Metawear_ICAD
{
    class MetawearSensor
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private BluetoothLEDevice device;
        private MbientLab.MetaWear.IMetaWearBoard board;
        private List<MetawearSensor> sensors = new List<MetawearSensor>();
        private OSC osc;

        public MainPage()
        {
            this.InitializeComponent();

            osc = new OSC();
            osc.outPort = 10000;

            foreach (HostName localHostName in NetworkInformation.GetHostNames())
            {
                if (localHostName.IPInformation != null)
                {
                    if (localHostName.Type == HostNameType.Ipv4)
                    {
                        oscIP.Text = localHostName.ToString();
                        break;
                    }
                }
            }

            osc.outIP = oscIP.Text;
            osc.Close();
            osc.Awake();

            for (int i = 0; i < 10; i++)
            {
                OscMessage msg = new OscMessage();
                msg.address = "/test";
                msg.values.Add("test");

                osc.Send(msg);
            }

            Scan();
        }

        private async void Scan()
        {
            var devices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector());

            foreach (DeviceInformation device in devices)
            {
                sensors.Add(new MetawearSensor { Name = device.Name, Address = device.Id});
            }

            MetawearMacAddresses.ItemsSource = sensors;

            if(sensors.Count() > 0)
            {
                MetawearMacAddresses.SelectedIndex = 0;
            }
        }

        private void oscIP_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox oscIP = (TextBox) sender;

            osc.outPort = 10000;

            System.Net.IPAddress temp;

            if (oscIP.Text.Split('.').Length == 4 && System.Net.IPAddress.TryParse(oscIP.Text, out temp))
            {
                osc.outIP = oscIP.Text;
                osc.Close();
                osc.Awake();

                for (int i = 0; i < 10; i++)
                {
                    OscMessage msg = new OscMessage();
                    msg.address = "/test";
                    msg.values.Add("test");

                    osc.Send(msg);
                }
            }
        }

        private async void Connect(object sender, RoutedEventArgs args)
        {
            int selection = MetawearMacAddresses.SelectedIndex;
            Debug.Print(sensors.ElementAt(selection).Address);
            device = await BluetoothLEDevice.FromIdAsync(sensors.ElementAt(selection).Address);
            board = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(device);

            ConnectionStatus.Text = "Connecting...";

            await board.InitializeAsync();

            ConnectionStatus.Text = "Connected.";

            ILed led = board.GetModule<ILed>();
            led.EditPattern(MbientLab.MetaWear.Peripheral.Led.Color.Green, MbientLab.MetaWear.Peripheral.Led.Pattern.Blink);
            led.Play();

            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;

            IAccelerometerBmi160 accelerometer = board.GetModule<IAccelerometerBmi160>();
            accelerometer.Configure(odr: MbientLab.MetaWear.Sensor.AccelerometerBmi160.OutputDataRate._100Hz, range: MbientLab.MetaWear.Sensor.AccelerometerBosch.DataRange._4g);

            await accelerometer.PackedAcceleration.AddRouteAsync(source => source.Stream(async data => await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                accelerationData.Text = data.Value<Acceleration>().ToString();
            })));

            IGyroBmi160 gyro = board.GetModule<IGyroBmi160>();
            gyro.Configure(odr: MbientLab.MetaWear.Sensor.GyroBmi160.OutputDataRate._100Hz, range: MbientLab.MetaWear.Sensor.GyroBmi160.DataRange._250dps);

            await gyro.PackedAngularVelocity.AddRouteAsync(source => source.Stream(async data => await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                gyroData.Text = data.Value<AngularVelocity>().ToString();

            })));

            IMagnetometerBmm150 magnetometer = board.GetModule<IMagnetometerBmm150>();
            magnetometer.Configure(preset: MbientLab.MetaWear.Sensor.MagnetometerBmm150.Preset.HighAccuracy);

            await magnetometer.PackedMagneticField.AddRouteAsync(source => source.Stream(async data => await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                magnetometerData.Text = data.Value<MagneticField>().ToString();
            })));

            accelerometer.PackedAcceleration.Start();
            gyro.PackedAngularVelocity.Start();
            magnetometer.PackedMagneticField.Start();

            accelerometer.Start();
            gyro.Start();
            magnetometer.Start();
        }

        private async void Disconnect(object sender, RoutedEventArgs args)
        {
            ILed led = board.GetModule<ILed>();
            led.Stop(true);
            board.TearDown();
            device = null;

            ConnectionStatus.Text = "Disconnected.";

            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
        }
    }
}
