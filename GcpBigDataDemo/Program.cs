using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.BigQuery.V2;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Storage.V1;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;

namespace GcpBigDataDemo;

class Program
{
    private static string? _projectId;

    static async Task Main(string[] args)
    {
        // Load project ID from environment variable
        _projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
        if (string.IsNullOrEmpty(_projectId))
        {
            Console.WriteLine("❌ Please set GOOGLE_PROJECT_ID environment variable.");
            return;
        }

        Console.WriteLine($"✅ Using GCP Project: {_projectId}");
        Console.WriteLine();

        // ------------------------------------------------------------
        // 1. BIGQUERY – Query a public dataset
        // ------------------------------------------------------------
        Console.WriteLine("📊 BIGQUERY DEMO");
        Console.WriteLine("----------------");
        await QueryBigQueryAsync();
        Console.WriteLine();

        // ------------------------------------------------------------
        // 2. PUB/SUB – Publish and subscribe to messages
        // ------------------------------------------------------------
        Console.WriteLine("📨 PUB/SUB DEMO");
        Console.WriteLine("---------------");
        await PubSubDemoAsync();
        Console.WriteLine();

        // ------------------------------------------------------------
        // 3. CLOUD STORAGE – Upload and download files
        // ------------------------------------------------------------
        Console.WriteLine("☁️  CLOUD STORAGE DEMO");
        Console.WriteLine("---------------------");
        await CloudStorageDemoAsync();
        Console.WriteLine();

        Console.WriteLine("✅ All demos completed. Press any key to exit...");
        Console.ReadKey();
    }

    // ================================================================
    // 1. BIGQUERY DEMO
    // ================================================================
    static async Task QueryBigQueryAsync()
    {
        try
        {
            // Create BigQuery client
            var client = BigQueryClient.Create(_projectId);

            // Query a public dataset – top 10 most-viewed Stack Overflow questions about google-bigquery
            string query = @"
                SELECT 
                    CONCAT('https://stackoverflow.com/questions/', CAST(id AS STRING)) as url,
                    view_count,
                    title
                FROM `bigquery-public-data.stackoverflow.posts_questions`
                WHERE tags LIKE '%google-bigquery%'
                ORDER BY view_count DESC
                LIMIT 10";

            Console.WriteLine("🔍 Running query on public Stack Overflow dataset...");
            var result = client.ExecuteQuery(query, parameters: null);

            Console.WriteLine("📈 Top 10 most-viewed Stack Overflow questions about google-bigquery:");
            int rank = 1;
            foreach (var row in result)
            {
                string url = (string)row["url"];
                long? views = (long?)row["view_count"];
                string title = (string)row["title"];
                Console.WriteLine($"  {rank}. [{views} views] {title}");
                Console.WriteLine($"     {url}");
                rank++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ BigQuery error: {ex.Message}");
        }
    }

    // ================================================================
    // 2. PUB/SUB DEMO
    // ================================================================
    static async Task PubSubDemoAsync()
    {
        try
        {
            string topicId = "test-topic-dotnet-demo";
            string subscriptionId = "test-subscription-dotnet-demo";

            // Create a publisher client
            var topicName = new TopicName(_projectId, topicId);
            var publisher = await PublisherClient.CreateAsync(topicName);

            // Create a subscriber client
            var subscriptionName = new SubscriptionName(_projectId, subscriptionId);
            var subscriber = await SubscriberClient.CreateAsync(subscriptionName);

            // Publish a message
            string messageText = $"Hello from .NET 6 at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            var pubsubMessage = new PubsubMessage
            {
                Data = Encoding.UTF8.GetBytes(messageText)
            };
            string messageId = await publisher.PublishAsync(pubsubMessage);
            Console.WriteLine($"📤 Published message: '{messageText}' (ID: {messageId})");

            // Pull and acknowledge messages (with a short timeout)
            var pulledMessages = new List<PubsubMessage>();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await subscriber.StartAsync(async (msg, cancellationToken) =>
                {
                    pulledMessages.Add(msg);
                    Console.WriteLine($"📥 Received: {Encoding.UTF8.GetString(msg.Data.ToArray())}");
                    return SubscriberClient.Reply.Ack;
                });

                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected after timeout
            }
            finally
            {
                await subscriber.StopAsync(cts.Token);
            }

            if (pulledMessages.Count == 0)
            {
                Console.WriteLine("⚠️  No messages received (subscription may not exist or is empty).");
                Console.WriteLine("   To create the subscription, run this in your GCP Console or gcloud:");
                Console.WriteLine($"   gcloud pubsub subscriptions create {subscriptionId} --topic={topicId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Pub/Sub error: {ex.Message}");
        }
    }

    // ================================================================
    // 3. CLOUD STORAGE DEMO
    // ================================================================
    static async Task CloudStorageDemoAsync()
    {
        try
        {
            string bucketName = $"{_projectId}-demo-bucket";
            string objectName = "sample-file.txt";
            string content = $"This is a sample file created by .NET 6 at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            // Create Storage client
            var storage = StorageClient.Create();

            // Ensure bucket exists (create if not)
            try
            {
                await storage.GetBucketAsync(bucketName);
                Console.WriteLine($"📦 Bucket '{bucketName}' already exists.");
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"📦 Creating bucket '{bucketName}'...");
                await storage.CreateBucketAsync(_projectId, bucketName);
                Console.WriteLine($"✅ Bucket '{bucketName}' created.");
            }

            // Upload a file (as a string)
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var uploaded = await storage.UploadObjectAsync(bucketName, objectName, "text/plain", memoryStream);
            Console.WriteLine($"📤 Uploaded '{objectName}' to bucket '{bucketName}' (size: {uploaded.Size} bytes)");

            // Download and read the file back
            using var downloadStream = new MemoryStream();
            await storage.DownloadObjectAsync(bucketName, objectName, downloadStream);
            downloadStream.Position = 0;
            using var reader = new StreamReader(downloadStream);
            string downloadedContent = await reader.ReadToEndAsync();
            Console.WriteLine($"📥 Downloaded '{objectName}':");
            Console.WriteLine($"   \"{downloadedContent}\"");

            // Optional: list all objects in the bucket
            var objects = await storage.ListObjectsAsync(bucketName, "").ToListAsync();
            Console.WriteLine($"📋 Total objects in bucket: {objects.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Cloud Storage error: {ex.Message}");
        }
    }
}
