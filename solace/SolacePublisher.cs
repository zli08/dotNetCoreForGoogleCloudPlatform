using System;
using System.Text;
using SolaceSystems.Solclient.Messaging;

namespace SolaceGcpPublisher
{
    class Program
    {
        static void Main(string[] args)
        {
            // Solace connection properties (from GCP Solace Cloud or your VM)
            var host = "your-broker-host.gcp.solace.cloud:55555"; // or your GCP VM IP
            var vpnName = "your-vpn-name";
            var username = "your-username";
            var password = "your-password";

            // Initialize Solace context
            ContextFactoryProperties contextProps = new ContextFactoryProperties()
            {
                SolClientLogLevel = SolLogLevel.Warning
            };
            ContextFactory.Instance.Init(contextProps);

            // Create and connect session
            using (IContext context = ContextFactory.Instance.CreateContext(new ContextProperties(), null))
            using (ISession session = context.CreateSession(
                new SessionProperties()
                {
                    Host = host,
                    VPNName = vpnName,
                    UserName = username,
                    Password = password,
                    ReconnectRetries = 3
                },
                null,
                null))
            {
                ReturnCode returnCode = session.Connect();
                if (returnCode != ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine($"Connection failed: {returnCode}");
                    return;
                }

                Console.WriteLine("Connected to Solace on GCP!");

                // Create and send a message
                using (IMessage message = ContextFactory.Instance.CreateMessage())
                {
                    message.Destination = ContextFactory.Instance.CreateTopic("orders/new");
                    message.BinaryAttachment = Encoding.UTF8.GetBytes("Hello from .NET on GCP!");
                    message.DeliveryMode = MessageDeliveryMode.Direct; // or Persistent

                    ReturnCode sendResult = session.Send(message);
                    Console.WriteLine(sendResult == ReturnCode.SOLCLIENT_OK
                        ? "Message published successfully!"
                        : $"Publish failed: {sendResult}");
                }

                session.Disconnect();
            }

            ContextFactory.Instance.Cleanup();
        }
    }
}
