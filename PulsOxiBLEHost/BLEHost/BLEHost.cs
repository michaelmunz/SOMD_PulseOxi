using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using System.Threading;
using System.IO.Pipes;
using System.Collections.Concurrent;

namespace BLEHost
{
    class BLEHost
    {
        private Windows.Foundation.IAsyncOperation<BluetoothLEDevice> bluetoothLEDevice;
        private DeviceInformation devInfo = null;        
        private GattCharacteristic selectedCharacteristic = null;
        private const int NEW_LINE_FLAG = 254;
        private List<Byte> cachedData = new List<Byte>();
        private Semaphore bleDevSemaphore = new Semaphore(1, 1);
        private static Guid ResultCharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f4");
        private NamedPipeServerStream pipeServer = null;
        private BlockingCollection<byte[]> outputQueue = new BlockingCollection<byte[]>();

        private Thread pipeServerThread;

        static void Main(string[] args)
        {

            // Start the program
            var host = new BLEHost();

            host.connect();

            // Close on key press
            Console.ReadLine();
        }


        public void connect()
        {

            bool found = searchForDevice();
            Console.WriteLine("Device found: " + found);
            bool connected = false;
            while (!connected)
            {
                connected = connectToDevice();
            }
            Console.WriteLine("Device connected: " + connected);


            pipeServerThread = new Thread(runPipeServer);
            pipeServerThread.Start();
            
        }
    

        private void runPipeServer()
        {
            while (true)
            {
                pipeServer = new NamedPipeServerStream("ble_host_pipe", PipeDirection.Out, 1);
                Console.WriteLine("Waiting for connection on pipe ble_host_pipe...");
                // Wait for a client to connect
                pipeServer.WaitForConnection();

                Console.WriteLine("Client connected");
                System.IO.StreamWriter writer = new System.IO.StreamWriter(pipeServer);
                writer.AutoFlush = true;
                while (true)
                {
                    try
                    {
                        byte[] data = outputQueue.Take();
                        StringBuilder sb = new StringBuilder();
                        foreach (byte b in data)
                        {
                            sb.AppendFormat("{0:d2} ", b);
                        }

                        String strdata = sb.ToString();
                        strdata = strdata.Trim();
                        writer.WriteLine(strdata);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Error during writing to pipe. Please reconnect.");                        
                        break;
                    }
                }
                pipeServer.Close();
            }
            
        }

        public bool searchForDevice()
        {
            bleDevSemaphore.WaitOne();            
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx

            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };



            DeviceWatcher deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;

            Console.WriteLine("Searching for device...");
            // Start the watcher.
            deviceWatcher.Start();

            bleDevSemaphore.WaitOne();
            Task<BluetoothLEDevice> bleDevTask = bluetoothLEDevice.AsTask<BluetoothLEDevice>();
            bleDevTask.Wait();
                        
            BluetoothLEDevice device = bleDevTask.GetAwaiter().GetResult();
            return device != null;
            
            

        }

        private static ushort convertUUIDToServiceName(Guid uuid)
        {
            var bytes = uuid.ToByteArray();
            var shortUuid = (ushort)(bytes[0] | (bytes[1] << 8));
            return shortUuid;
        }

        public bool connectToDevice()
        {
            Task<Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult> serviceTask = bluetoothLEDevice.GetResults().GetGattServicesAsync().AsTask< Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult>();
            serviceTask.Wait();
            Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult services = serviceTask.GetAwaiter().GetResult();
            GattDeviceService srvToUse = null;
            for (int i = 0; i < services.Services.Count; i++)
            {
                GattDeviceService srv = services.Services[i];
                //Console.WriteLine("UUID: " + srv.Uuid+"("+ convertUUIDToServiceName(srv.Uuid)+")");
                if (convertUUIDToServiceName(srv.Uuid) == 65504)
                {
                    Console.WriteLine("Service found.");
                    srvToUse = srv;
                    break;
                }

                //65504, char: 65508
            }
            if (srvToUse == null)
            {
                return false;
            }

            Task<GattCharacteristicsResult> characteristicsTask = srvToUse.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask<GattCharacteristicsResult>();
            characteristicsTask.Wait();

            IReadOnlyList<GattCharacteristic> characteristics = characteristicsTask.GetAwaiter().GetResult().Characteristics;

            foreach (GattCharacteristic c in characteristics)
            {
                Console.WriteLine("C UUID: " + c.Uuid + "(" + convertUUIDToServiceName(c.Uuid) + ")");
                if (convertUUIDToServiceName(c.Uuid) == 65508){
                    Console.WriteLine("Characteristics found.");
                    selectedCharacteristic = c;
                }
            }


            if (selectedCharacteristic == null)
            {
                Console.WriteLine("Error: no characteristics found. ");
                return false;
            }

            // we do not get any presentation format.

            Task<GattCommunicationStatus> statusTask = selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask<GattCommunicationStatus>();
            statusTask.Wait();            
            
            selectedCharacteristic.ValueChanged += OnValueChanged;
            return true;
        }

        private async void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {            
            cacheData(args.CharacteristicValue);
        }


        
        private void cacheData(IBuffer buffer)
        {            
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);

            for(int i = 0; i < data.Length; i++)
            {
                byte v = data[i];
                // if we find 170, parse data and start a new line/cache
                if (v == NEW_LINE_FLAG)
                {
                    sendData();
                    cachedData.Clear();
                }
                // if not NEW_LINE_FLAG, keep looking but store each value in our cache so we can use it once we find our number
                cachedData.Add(v);                
            }
            
        }

        private void sendData()
        {
            StringBuilder sb = new StringBuilder();
            /*sb.AppendFormat("({0:d}) [", cachedData.Count);
            foreach (byte b in cachedData)
            {
                //sb.AppendFormat("{0:x2}", b);
                sb.AppendFormat("{0:d2}, ", b);
            }
            sb.Append("]");
            */
            foreach (byte b in cachedData)
            {                
                sb.AppendFormat("{0:d2} ", b);
            }
            
            String str = sb.ToString();
            Console.WriteLine(str);

            outputQueue.Add(cachedData.ToArray());

        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfo) {             
            if (deviceInfo.Id.Equals(devInfo.Id))
            {
                Console.WriteLine("Update info for device.");

                
            }

        
        }
        
        private string FormatValue(IBuffer buffer)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);
            if (data != null)
            {
                if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
                {
                    try
                    {
                        // battery level is encoded as a percentage value in the first byte according to
                        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                        return "Battery Level: " + data[0].ToString() + "%";
                    }
                    catch (ArgumentException)
                    {
                        return "Battery Level: (unable to parse)";
                    }
                }
                
                try
                {
                    StringBuilder sb = new StringBuilder(data.Length * 2);
                    sb.AppendFormat("({0:d}) [", data.Length);
                    foreach (byte b in data)
                    {
                        //sb.AppendFormat("{0:x2}", b);
                        sb.AppendFormat("{0:d2}, ", b);
                    }
                    sb.Append("]");
                    return sb.ToString();
                }
                catch (ArgumentException)
                {
                    return "Unknown format";
                }
            }
            else
            {
                return "Empty data received";
            }            
        }
        

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {            
            if (deviceInfo.Name.Equals("VTM 20F"))
            {
                Console.WriteLine("Found device: " + deviceInfo.Id + ", Name: " + deviceInfo.Name);
                devInfo = deviceInfo;
                bluetoothLEDevice = BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
                bleDevSemaphore.Release();
            }
            else
            {
                //Console.WriteLine("Found other device: " + deviceInfo.Id + ", Name: " + deviceInfo.Name);
            }
            
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            Console.WriteLine("Finished enumeration");
        }

        

    }
}
