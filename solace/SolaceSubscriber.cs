using System;
using System.Text;
using System.Threading;
using SolaceSystems.Solclient.Messaging;

namespace SolaceGcpSubscriber
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = "your-broker-host.gcp.solace.cloud:55555";
            var vpnName = "your-vpn-name";
            var username = "your-username";
            var password = "your-password";

            ContextFactoryProperties contextProps = new ContextFactoryProperties()
            {
                SolClientLogLevel = SolLogLevel.Warning
            };
            ContextFactory.Instance.Init(contextProps);

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
                HandleMessageEvent,   // Message event handler
                HandleSessionEvent))  // Session event handler
            {
                ReturnCode returnCode = session.Connect();
                if (returnCode != ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine($"Connection failed: {returnCode}");
                    return;
                }

                Console.WriteLine("Connected to Solace on GCP!");

                // Subscribe to a topic
                ITopic topic = ContextFactory.Instance.CreateTopic("orders/new");
                session.Subscribe(topic, SubscribeFlag.WaitForConfirm, null);

                Console.WriteLine("Subscribed to 'orders/new'. Waiting for messages...");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();

                session.Unsubscribe(topic, UnsubscribeFlag.WaitForConfirm, null);
                session.Disconnect();
            }

            ContextFactory.Instance.Cleanup();
        }

        // Handle incoming messages
        private static void HandleMessageEvent(object source, MessageEventArgs args)
        {
            using (IMessage message = args.Message)
            {
                string payload = Encoding.UTF8.GetString(message.BinaryAttachment);
                Console.WriteLine($"Received message on topic '{message.Destination.Name}': {payload}");
            }
        }

        // Handle session events (connect, disconnect, errors)
        private static void HandleSessionEvent(object source, SessionEventArgs args)
        {
            Console.WriteLine($"Session Event: {args.Event} - {args.Info}");
        }
    }
}
