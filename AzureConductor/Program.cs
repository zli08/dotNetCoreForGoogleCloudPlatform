using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Azure.Monitor.Ingestion;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;

class Program
{
    // Configuration placeholders
    private const string SubscriptionId = "<your-subscription-id>";
    private const string ResourceGroupName = "<your-resource-group>";
    private const string VmName = "<your-vm-name>";

    private const string EventGridTopicEndpoint = "https://<your-topic>.<region>-1.eventgrid.azure.net/api/events";

    private const string ServiceBusConnectionString = "<your-servicebus-connection-string>";
    private const string ServiceBusQueueName = "my-queue";

    private const string MonitorDceUri = "https://<your-dce>.<region>.ingest.monitor.azure.com";
    private const string MonitorDcrId = "<your-data-collection-rule-id>";
    private const string MonitorStreamName = "Custom-MyAppLogs";

    private const string FunctionAppUrl = "https://<your-function-app>.azurewebsites.net/api/MyHttpTrigger";

    static async Task Main(string[] args)
    {
        var credential = new DefaultAzureCredential();

        Console.WriteLine("Starting Azure integrations...");

        await CheckVirtualMachineStatusAsync(credential);
        await PublishEventGridEventAsync(credential);
        await SendServiceBusMessageAsync();
        await IngestAzureMonitorLogAsync(credential);
        await TriggerAzureFunctionAsync();

        Console.WriteLine("All integrations completed.");
    }

    // 1. Virtual Machines (Management Plane)
    static async Task CheckVirtualMachineStatusAsync(TokenCredential credential)
    {
        Console.WriteLine("\n--- Checking VM Status ---");
        var armClient = new ArmClient(credential);
        var vmResourceId = VirtualMachineResource.CreateResourceIdentifier(SubscriptionId, ResourceGroupName, VmName);
        var vm = armClient.GetVirtualMachineResource(vmResourceId);

        var instanceView = await vm.GetInstanceViewAsync();
        var powerState = instanceView.Value.Statuses
            .FirstOrDefault(s => s.Code != null && s.Code.StartsWith("PowerState/"))
            ?.DisplayStatus;

        Console.WriteLine($"VM '{VmName}' power state: {powerState ?? "Unknown"}");
    }

    // 2. Event Grid (Publishing Events)
    static async Task PublishEventGridEventAsync(TokenCredential credential)
    {
        Console.WriteLine("\n--- Publishing Event Grid Event ---");
        var client = new EventGridPublisherClient(new Uri(EventGridTopicEndpoint), credential);

        var eventItem = new EventGridEvent(
            subject: "app/vm-status-check",
            eventType: "VmStatus.Checked",
            dataVersion: "1.0",
            data: new { Message = "VM status checked successfully", Timestamp = DateTime.UtcNow }
        )
        {
            Id = Guid.NewGuid().ToString()
        };

        await client.SendEventAsync(eventItem);
        Console.WriteLine("Event published to Event Grid.");
    }

    // 3. Service Bus (Messaging)
    static async Task SendServiceBusMessageAsync()
    {
        Console.WriteLine("\n--- Sending Service Bus Message ---");
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender(ServiceBusQueueName);

        var message = new ServiceBusMessage(JsonSerializer.Serialize(new
        {
            Action = "ProcessVmData",
            ProcessedAt = DateTime.UtcNow
        }));

        await sender.SendMessageAsync(message);
        Console.WriteLine("Message sent to Service Bus.");
    }

    // 4. Azure Monitor (Log Ingestion)
    static async Task IngestAzureMonitorLogAsync(TokenCredential credential)
    {
        Console.WriteLine("\n--- Ingesting Azure Monitor Log ---");
        var client = new LogsIngestionClient(new Uri(MonitorDceUri), credential);

        var logRecord = new Dictionary<string, object>
        {
            { "TimeGenerated", DateTime.UtcNow },
            { "Level", "Information" },
            { "Message", "Application workflow completed." },
            { "Service", "IntegrationApp" }
        };

        // UploadAsync expects a list of records (or BinaryData)
        await client.UploadAsync(MonitorDcrId, MonitorStreamName, new List<Dictionary<string, object>> { logRecord });
        Console.WriteLine("Custom log ingested to Azure Monitor.");
    }

    // 5. Azure Functions (HTTP Trigger)
    static async Task TriggerAzureFunctionAsync()
    {
        Console.WriteLine("\n--- Triggering Azure Function ---");
        using var httpClient = new HttpClient();

        // Note: In production, secure this with a function key or Managed Identity
        var payload = JsonSerializer.Serialize(new { VmName = VmName, Status = "Processed" });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(FunctionAppUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Azure Function triggered successfully. Response: {result}");
        }
        else
        {
            Console.WriteLine($"Failed to trigger Function. Status: {response.StatusCode}");
        }
    }
}