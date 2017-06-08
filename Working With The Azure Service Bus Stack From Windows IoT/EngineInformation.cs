using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Eldert.IoT.RaspberryPi2.FieldHub
{
    /// <summary>
    /// Class representing the information an engine would send out.
    /// To be able to serialize, we have to annotate class and it's members.
    /// </summary>
    [DataContract]
    internal class EngineInformation
    {
        [DataMember]
        internal Guid Identifier;

        [DataMember]
        internal string ShipName;

        [DataMember]
        internal string EngineName;

        [DataMember]
        internal double Temperature;

        [DataMember]
        internal double RPM;

        [DataMember]
        internal bool Warning;

        [DataMember]
        internal int EngineWarning;

        [DataMember]
        internal DateTime CreatedDateTime;
    }

    /// <summary>
    /// Class used to serialize engine information to JSON.
    /// Created this due to issues with the NewtonSoft JSON serializer, probably will work with a future version.
    /// </summary>
    internal static class EngineInformationSerialization
    {
        internal static string Serialize(this EngineInformation engineInformation)
        {
            // Create a stream to serialize the object to
            var memoryStream = new MemoryStream();

            // Serialize the object to the stream
            var jsonSerializer = new DataContractJsonSerializer(typeof(EngineInformation));
            jsonSerializer.WriteObject(memoryStream, engineInformation);
            memoryStream.Flush();
            var json = memoryStream.ToArray();
            return Encoding.UTF8.GetString(json, 0, json.Length);
        }
    }
}