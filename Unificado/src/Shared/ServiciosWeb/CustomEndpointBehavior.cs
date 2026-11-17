using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.ServiciosWeb
{
    internal class CustomEndpointBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(new CustomClientMessageInspector());
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }

    internal class CustomClientMessageInspector : IClientMessageInspector
    {
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            //var str = MessageToString(ref request);

            return null;
        }

        string MessageToString(ref Message message)
        {
            var mb = message.CreateBufferedCopy(int.MaxValue);

            // re-create the original message, because "copy" changes its state.
            message = mb.CreateMessage();

            var stream = new MemoryStream();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineHandling = NewLineHandling.Replace,
                NewLineChars = "\r\n",
                Encoding = Encoding.UTF8
            };

            var writer = XmlWriter.Create(stream, settings);

            mb.CreateMessage().WriteMessage(writer);
            writer.Flush();
            stream.Position = 0;

            //var data = new byte[stream.Length];
            //stream.Read(data, 0, (int)stream.Length);
            var data = stream.ToArray();

            if (data[0] != (byte)'<')
            {
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            }
            else
            {
                return Encoding.UTF8.GetString(data, 0, data.Length);
            }
        }

        Message CreateMessage(String xml, MessageVersion version)
        {
            var stream = new System.IO.MemoryStream();
            var writer = new System.IO.StreamWriter(stream);
            writer.Write(xml);
            writer.Flush();
            stream.Position = 0;
            
            var reader = XmlReader.Create(stream);


            return Message.CreateMessage(reader, int.MaxValue, version);
        }
    }
}