using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
using Sensors.Dht;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace TempSense
{
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer _dispatchTimer;
        private GpioPin _temperaturePin;
        private IDht _dhtInterface;
        private List<int> _retryCount;
        private DateTimeOffset _startedAt;
        public float Humidity { get; set; }
        public float Temperature { get; set; }

        private static readonly ResourceLoader ResourceFile = new ResourceLoader("secrets");
        private readonly string _influxDbUri = ResourceFile.GetString("InfluxDbUri");
        private readonly string _influxDbDatabaseName = ResourceFile.GetString("InfluxDbDatabaseName");
        private readonly string _influxDbUserName = ResourceFile.GetString("InfluxDbUserName");
        private readonly string _influxDbPassword = ResourceFile.GetString("InfluxDbPassword");


        public MainPage()
        {
            this.InitializeComponent();

            InitHardware();
            _dispatchTimer.Interval = TimeSpan.FromSeconds(5);
            _dispatchTimer.Tick += _dispatchTimer_Tick;

            //GPIO pin 4
            _temperaturePin = GpioController.GetDefault().OpenPin(4, GpioSharingMode.Exclusive);

            // create instance of a DHT11 
            _dhtInterface = new Dht11(_temperaturePin, GpioPinDriveMode.Input);

            _dispatchTimer.Start();
            _startedAt = DateTimeOffset.Now;
        }
        private void InitHardware()
        {
            _dispatchTimer = new DispatcherTimer();
            _temperaturePin = null;
            _dhtInterface = null;
            _retryCount = new List<int>();
            _startedAt = DateTimeOffset.Parse("1/1/1");
        }

        private async void _dispatchTimer_Tick(object sender, object e)
        {
            try
            {
                var reading = await _dhtInterface.GetReadingAsync().AsTask();
                reading.Temperature = (reading.Temperature * 9 / 5) + 32;
                if (reading.IsValid)
                {
                    Temperature = Convert.ToSingle(reading.Temperature);
                    Humidity = Convert.ToSingle(reading.Humidity);


                }
                else {
                    Debug.WriteLine(
                        $"Invalid Reading!" +
                        $"RetryCount: {reading.RetryCount}" +
                        $"TimedOut: {reading.TimedOut}" +
                        $"Humidity: {reading.Humidity}" +
                        $"Temperature: {reading.Temperature}");
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            var temp = new LineProtocolPoint(
                measurement: "°F",
                fields: new Dictionary<string, object>
                {
                    {"Temperature", Temperature }
                    
                },
                tags: new Dictionary<string, string>
                {
                    {"entity_id", "Pi-One" }
                });

            var humidity = new LineProtocolPoint(
                measurement: "%",
                fields: new Dictionary<string, object>
                {
                    {"Humidity", Humidity}
                },
                tags: new Dictionary<string, string>
                {
                    {"entity_id", "Pi-One"}
                });

            var payload = new LineProtocolPayload();
            payload.Add(temp);
            payload.Add(humidity);

            var client = new LineProtocolClient(new Uri(_influxDbUri), _influxDbDatabaseName, _influxDbUserName, _influxDbPassword);
            var influxResult = await client.WriteAsync(payload);
            if (!influxResult.Success)

                Debug.WriteLine(influxResult.ErrorMessage);
        }

    }
}