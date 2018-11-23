using MQTTnet;
using MQTTnet.Client;
using Sensors.Dht;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace TempSense
{
    public sealed partial class MainPage : Page
    {
        public float Humidity { get; set; }
        public float Temperature { get; set; }

        private DispatcherTimer _dispatchTimer;
        private GpioPin _temperaturePin;
        private IDht _dhtInterface;
        private List<int> _retryCount;
        private DateTimeOffset _startedAt;
        
        private static readonly ResourceLoader ResourceFile = new ResourceLoader("secrets");

        private readonly string _influxDbUri = ResourceFile.GetString("InfluxDbUri");
        private readonly string _influxDbDatabaseName = ResourceFile.GetString("InfluxDbDatabaseName");
        private readonly string _influxDbUserName = ResourceFile.GetString("InfluxDbUserName");
        private readonly string _influxDbPassword = ResourceFile.GetString("InfluxDbPassword");

        private readonly string _topicToPublishOn = ResourceFile.GetString("TopicRoot");
        private readonly string _mqttServer = ResourceFile.GetString("MqttServer");
        private readonly string _clientId = ResourceFile.GetString("ClientId");

        private static readonly MqttFactory MqttFactory = new MqttFactory();
        private readonly IMqttClient _mqttClient = MqttFactory.CreateMqttClient();

        private const int DelayBetweenReadingsInSeconds = 60;


        public MainPage()
        {
            InitializeComponent();
            SetupMqtt();
            InitHardware();

            _dispatchTimer.Interval = TimeSpan.FromSeconds(DelayBetweenReadingsInSeconds);
            _dispatchTimer.Tick += _dispatchTimer_Tick;
            _dispatchTimer.Start();
            _startedAt = DateTimeOffset.Now;

        }
        private void InitHardware()
        {
            _dispatchTimer = new DispatcherTimer();
            
            _retryCount = new List<int>();
            _startedAt = DateTimeOffset.Parse("1/1/1");

            //GPIO pin 4
            _temperaturePin = GpioController.GetDefault().OpenPin(4, GpioSharingMode.Exclusive);

            // create instance of a DHT11 
            _dhtInterface = new Dht11(_temperaturePin, GpioPinDriveMode.Input);
        }

        private async void SetupMqtt()
        {
            var mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId(_clientId)
                .WithTcpServer(_mqttServer)
                .WithCleanSession()
                .Build();

            var session = await _mqttClient.ConnectAsync(mqttOptions);

            if (!session.IsSessionPresent)
                Debug.WriteLine("Not connected to MQTT Broker");
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
                else
                {
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

            //disabling Influx Connection for now, using MQTT instead

            //var temperature = new LineProtocolPoint(
            //    measurement: "°F",
            //    fields: new Dictionary<string, object>
            //    {
            //        {"Temperature", Temperature }

            //    },
            //    tags: new Dictionary<string, string>
            //    {
            //        {"entity_id", "Pi-One" }
            //    });

            //var humidity = new LineProtocolPoint(
            //    measurement: "%",
            //    fields: new Dictionary<string, object>
            //    {
            //        {"Humidity", Humidity}
            //    },
            //    tags: new Dictionary<string, string>
            //    {
            //        {"entity_id", "Pi-One"}
            //    });

            //var payload = new LineProtocolPayload();
            //payload.Add(temperature);
            //payload.Add(humidity);

            //var client = new LineProtocolClient(new Uri(_influxDbUri), _influxDbDatabaseName, _influxDbUserName, _influxDbPassword);
            //var influxResult = await client.WriteAsync(payload);
            //if (!influxResult.Success)
            //    Debug.WriteLine(influxResult.ErrorMessage);

            var temperatureMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"{_topicToPublishOn}/temperature")
                .WithPayload(Temperature.ToString(CultureInfo.InvariantCulture))
                .WithExactlyOnceQoS()
                .WithRetainFlag()
                .Build();

            var humidityMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"{_topicToPublishOn}/humidity")
                .WithPayload(Humidity.ToString(CultureInfo.InvariantCulture))
                .WithExactlyOnceQoS()
                .WithRetainFlag()
                .Build();

            await _mqttClient.PublishAsync(temperatureMessage);
            await _mqttClient.PublishAsync(humidityMessage);
        }

    }
}