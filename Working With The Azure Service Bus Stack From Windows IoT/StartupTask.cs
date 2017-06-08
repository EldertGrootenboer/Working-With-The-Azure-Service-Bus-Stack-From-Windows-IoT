using System;
using System.Diagnostics;
using System.Text;

using Windows.ApplicationModel.Background;
using Windows.System.Threading;

using ppatierno.AzureSBLite.Messaging;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace Eldert.IoT.RaspberryPi2.FieldHub
{
    public sealed class StartupTask : IBackgroundTask
    {
        // Needed to make sure the application keeps running in the background
        private BackgroundTaskDeferral _backgroundTaskDeferral;

        private readonly EventHubClient _eventHub = EventHubClient.CreateFromConnectionString("Endpoint=sb://eldertiot.servicebus.windows.net/;SharedAccessKeyName=fieldhubs;SharedAccessKey=3SkPy94io8d+vMlbqtbHcsePVWuVcQ+nhaVPJl76sBk=", "eventhubfieldhubs");
        private readonly SubscriptionClient _topic = SubscriptionClient.CreateFromConnectionString("Endpoint=sb://eldertiot.servicebus.windows.net/;SharedAccessKeyName=fieldhubs;SharedAccessKey=Ex17AiYp7kCInnFbjrDwlp8QJYw0oxJ+jRPGOjau+AM=", "topicengineadministration", "Hydra");

        private readonly SHT15 _sht15 = new SHT15(24, 23);
        private bool _warningGenerated;
        private static int _maximumTemperature = 500;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // Do not close application after startup
            _backgroundTaskDeferral = taskInstance.GetDeferral();

            // Set task to be run in background
            ThreadPoolTimer.CreatePeriodicTimer(timer =>
            {
                GetSensorReadings();
            }, new TimeSpan(0, 0, 5));

            // Trigger for receiving messages from a topic
            _topic.OnMessage(message => {
                var newMaximumTemperature = message.Properties["maximumtemperature"];
                _maximumTemperature = Convert.ToInt32(newMaximumTemperature);
                Debug.WriteLine($"Maximum temperature has been set to {newMaximumTemperature}");
            });
        }

        /// <summary>
        /// Get readings from sensors.
        /// </summary>
        private void GetSensorReadings()
        {
            // Get temperature from SHT15
            // To simulate more realistic engine temperatures, we multiply it
            var temperature = _sht15.CalculateTemperatureC(_sht15.ReadRawTemperature()) * 20;
            Debug.WriteLine($"Temperature: {temperature}");

            // Check if a warning should be generated
            var warning = temperature > _maximumTemperature;

            try
            {
                // Create engine information object, simulate some of the input
                var engineInformation = new EngineInformation
                {
                    Identifier = Guid.NewGuid(),
                    ShipName = "Hydra",
                    EngineName = "Main Engine Port",
                    CreatedDateTime = DateTime.UtcNow,
                    RPM = new Random().Next(400, 1000),
                    Temperature = temperature,
                    Warning = warning,
                    EngineWarning = !_warningGenerated && new Random().Next(0, 10) > 9 ? new Random().Next(1, 3) : 0
                };

                // Check if a warning was generated
                if (engineInformation.EngineWarning > 0)
                {
                    _warningGenerated = true;
                    Debug.WriteLine($"EngineWarning sent: {engineInformation.EngineWarning}");
                }

                // Serialize to JSON
                // With the current version (7.0.1) the Newtonsoft JSON serializer does not work, so created our own serializer
                var serializedString = engineInformation.Serialize();

                // Create brokered message
                // Send with shipname as partitionkey, to make sure messages for 1 ship are processed in correct order
                var message = new EventData(Encoding.UTF8.GetBytes(serializedString));
                message.Properties.Add("haswarning", engineInformation.Warning);
                message.PartitionKey = engineInformation.ShipName;

                // Send to event hub
                _eventHub.Send(message);
            }
            catch (Exception exception)
            {
                exception.Log();
            }
        }
    }
}
