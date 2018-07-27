using System;
using System.Threading;

using Windows.Devices.Gpio;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

using nanoFramework.Targets.NETDUINO3_WIFI;

namespace nfAirQualityReader
{
    public class Program
    {
        #region The Locals

        // Heartbeat LED
        private static GpioPin _ledUser;

        // The Particle Matter sensor requires 9600,8,N,1 on serial
        private static SerialDevice _sds011;

        #endregion

        public static void Main()
        {
            try
            {
                Console.WriteLine("\rHello world of nanoFramework!\r");

                // Setup the signal LED
                InitGpio();

                // Setup the serial communication with the sensor
                InitSensor();

                // And watch your console output
                for (; ; )
                {
                    _ledUser.Write(_ledUser.Read() == GpioPinValue.Low ? GpioPinValue.High : GpioPinValue.Low);
                    Thread.Sleep(500);
                }
            }
            catch (Exception ex)
            {
                // Don't leave thread 
                Console.WriteLine(ex.ToString());

                for (; ; )
                {
                    _ledUser.Write(_ledUser.Read() == GpioPinValue.Low ? GpioPinValue.High : GpioPinValue.Low);
                    Thread.Sleep(100);
                }
            }
        }

        private static void InitSensor()
        {
            // Since we only do a read the sensor on COM6 on pin D1 = PC06 = TX and D0 = PC07 = RX

            //Open the port
            _sds011 = SerialDevice.FromId("COM6");

            // Set the usual stuff required for a serial connection
            _sds011.BaudRate = 9600;
            _sds011.DataBits = 8;
            _sds011.Parity = SerialParity.None;
            _sds011.StopBits = SerialStopBitCount.One;
            _sds011.Handshake = SerialHandshake.None;

            // Timeouts of 5 secs
            _sds011.ReadTimeout = new TimeSpan(0, 0, 5);

            // Reader for the output/input streams
            DataReader reader = new DataReader(_sds011.InputStream);

            // Set a watch for the return character
            _sds011.WatchChar = (char)0xAB;

            // Now register for the data received event
            _sds011.DataReceived += _sds011_DataReceived;
        }

        private static void _sds011_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType == SerialData.Chars)
            {
                // Ignore the in between noise
            }
            else if (e.EventType == SerialData.WatchChar)
            {
                SerialDevice serDev = (SerialDevice)sender;

                using (DataReader dr = new DataReader(serDev.InputStream))
                {
                    dr.InputStreamOptions = InputStreamOptions.Partial;
                    uint bytesRead = dr.Load(serDev.BytesToRead);

                    if (bytesRead > 0)
                    {
                        byte[] rawData = new byte[bytesRead];
                        dr.ReadBytes(rawData);

                        // If rawData.Length == 10 and rawData[0] = 0xAA and rawData[9] = 0xAB and rawData[1] = 0xC0
                        // this means we have a valid measure package from the sensor
                        // and byte[2] = low byte, byte[3] = high byte of uint representing the AQI for PM 2.5
                        if (rawData.Length >= 10)
                        {
                            if ((rawData[0] == 0xAA) && (rawData[1] == 0xC0) && (rawData[9] == 0xAB))
                            {
                                // Need to do checksum
                                byte crc = 0;
                                for (int i = 0; i < 6; i++)
                                {
                                    crc += rawData[i + 2];
                                }
                                if (crc == rawData[8])
                                {
                                    // All right, we have a go !!!!
                                    float pm25 = 0, pm10 = 0;

                                    pm25 = (float)((int)rawData[2] | (int)(rawData[3] << 8)) / 10;
                                    pm10 = (float)((int)rawData[4] | (int)(rawData[5] << 8)) / 10;

                                    Console.WriteLine(String.Format("Air quality index: {0}\tPM 10\t{1} µg / m3\tPM 2.5\t{2} µg / m3\tSensor: {3}",
                                        DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'\t'HH':'mm':'ss"),
                                        pm10.ToString("N1"),
                                        pm25.ToString("N1"),
                                        "SDS011"));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void InitGpio()
        {
            _ledUser = GpioController.GetDefault().OpenPin(Gpio.UserLed);
            _ledUser.SetDriveMode(GpioPinDriveMode.Output);
        }
    }
}
