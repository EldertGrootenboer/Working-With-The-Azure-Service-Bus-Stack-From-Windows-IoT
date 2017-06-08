using System;
using System.Diagnostics;

using ppatierno.AzureSBLite.Messaging;

namespace Eldert.IoT.RaspberryPi2.FieldHub
{
    /// <summary>
    /// Class used to handle exceptions.
    /// </summary>
    internal static class Exceptions
    {
        private static QueueClient _queue;

        private static QueueClient Queue => _queue ?? (_queue = QueueClient.CreateFromConnectionString("Endpoint=sb://eldertiot.servicebus.windows.net/;SharedAccessKeyName=fieldhubs;SharedAccessKey=EDfKFHmka4Dc0DpqigecDAxpoy+85s6KZASrY7LZ2cA=", "queueerrorsandwarnings"));

        public static void WriteToServiceBusQueue(this Exception exception)
        {
            var message = new BrokeredMessage();
            message.Properties["ship"] = "Hydra";
            message.Properties["time"] = DateTime.UtcNow;
            message.Properties["exceptionmessage"] = exception.ToString();
            Queue.Send(message);
        }

        public static void Log(this Exception exception)
        {
            Debug.WriteLine(exception.ToString());
            exception.WriteToServiceBusQueue();
        }
    }
}
