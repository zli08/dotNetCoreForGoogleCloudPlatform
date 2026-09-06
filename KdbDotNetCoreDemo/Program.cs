using System;
using System.Text;
using kx;

namespace KdbDotNetCoreDemo;

class Program
{
    static void Main(string[] args)
    {
        c? connection = null;

        try
        {
            // 1. Establish connection to kdb+ process
            //    Assumes a kdb+ instance is running on localhost:5001
            //    e.g. start with: q -p 5001
            connection = new c("localhost", 5001);
            connection.ReceiveTimeout = 5000;
            connection.e = Encoding.UTF8;

            Console.WriteLine("✅ Connected to kdb+ on localhost:5001");
            Console.WriteLine();

            // 2. Execute a simple q expression (synchronous)
            //    The k() method sends a sync request and returns the result
            object result = connection.k("2 + 3");
            Console.WriteLine($"2 + 3 = {result}");

            // 3. Execute a q expression that returns a string
            //    Demonstrates encoding and decoding of Unicode
            object unicodeResult = connection.k("`$ \"c\" $ 0x52616e627920426ac3b6726b6c756e64204142");
            Console.WriteLine($"Unicode result: {unicodeResult}");

            Console.WriteLine();

            // 4. Query a table (assumes a 'trade' table exists)
            //    If no trade table exists, this will throw an exception
            try
            {
                object tableResult = connection.k("select sym, price, size from trade where sym=`AAPL");
                Flip flip = c.td(tableResult);  // Convert to Flip (columnar table)

                Console.WriteLine($"📊 Query returned {c.n(flip.y?[0] ?? Array.Empty<object>())} rows");
                Console.WriteLine($"Columns: {string.Join(", ", flip.x ?? Array.Empty<string>())}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Table query failed (table may not exist): {ex.Message}");
            }

            Console.WriteLine();

            // 5. Insert data into a table
            //    Creates a single row of trade data
            object[] row = new object[]
            {
                DateTime.Now.TimeOfDay,  // time
                "AAPL",                  // sym
                175.5,                   // price
                1000                     // size
            };

            try
            {
                connection.k("insert", "trade", row);
                Console.WriteLine("✅ Inserted one row into 'trade' table");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Insert failed (table may not exist): {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
        finally
        {
            // Always close the connection
            connection?.Close();
            Console.WriteLine("🔌 Connection closed.");
        }
    }
}
