using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace HeaderDeserializerPoc
{
    [Serializable]
    public sealed class DemoHeaderValue
    {
        public string User { get; set; }
        public string Role { get; set; }
        public DateTime IssuedAtUtc { get; set; }

        public override string ToString()
        {
            return $"User={User}, Role={Role}, IssuedAtUtc={IssuedAtUtc:o}";
        }
    }

    internal static class Program
    {
        private const string Prefix = "http://localhost:5055/";
        private const string HeaderName = "X-Demo-Object";

        private static void Main()
        {
            Console.WriteLine("Starting local PoC server.");
            Console.WriteLine($"Listening on {Prefix}");
            Console.WriteLine();
            Console.WriteLine("Routes:");
            Console.WriteLine("  GET /sample       - returns a safe demo header value");
            Console.WriteLine("  GET /deserialize  - deserializes the X-Demo-Object header");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop.");
            Console.WriteLine();

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(Prefix);
                listener.Start();

                while (true)
                {
                    var context = listener.GetContext();
                    Handle(context);
                }
            }
        }

        private static void Handle(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url.AbsolutePath.Trim('/').ToLowerInvariant();

                switch (path)
                {
                    case "sample":
                        HandleSample(context);
                        break;

                    case "deserialize":
                        HandleDeserialize(context);
                        break;

                    default:
                        WriteText(
                            context,
                            404,
                            "Use /sample to generate a demo header, then /deserialize with X-Demo-Object."
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteText(context, 500, "Server error:\n" + ex);
            }
        }

        private static void HandleSample(HttpListenerContext context)
        {
            var value = new DemoHeaderValue
            {
                User = "alice@example.test",
                Role = "DemoUser",
                IssuedAtUtc = DateTime.UtcNow
            };

            string headerValue = SerializeToBase64Header(value);

            var response = new StringBuilder();
            response.AppendLine("Safe demo header value:");
            response.AppendLine();
            response.AppendLine(headerValue);
            response.AppendLine();
            response.AppendLine("Example curl command:");
            response.AppendLine();
            response.AppendLine(
                $"curl -H \"{HeaderName}: {headerValue}\" {Prefix}deserialize"
            );

            WriteText(context, 200, response.ToString());
        }

        private static void HandleDeserialize(HttpListenerContext context)
        {
            string headerValue = context.Request.Headers[HeaderName];

            if (string.IsNullOrWhiteSpace(headerValue))
            {
                WriteText(context, 400, $"Missing required header: {HeaderName}");
                return;
            }

            object deserialized = DeserializeFromBase64Header(headerValue);

            var response = new StringBuilder();
            response.AppendLine("Deserialized object from header.");
            response.AppendLine();
            response.AppendLine("This is the dangerous behavior being demonstrated.");
            response.AppendLine();
            response.AppendLine($"Runtime type: {deserialized.GetType().FullName}");
            response.AppendLine($"Value: {deserialized}");

            WriteText(context, 200, response.ToString());
        }

        private static string SerializeToBase64Header(object value)
        {
            var serializer = new NetDataContractSerializer();

            using (var memory = new MemoryStream())
            {
                serializer.Serialize(memory, value);
                return Convert.ToBase64String(memory.ToArray());
            }
        }

        private static object DeserializeFromBase64Header(string headerValue)
        {
            byte[] bytes = Convert.FromBase64String(headerValue);

            using (var memory = new MemoryStream(bytes))
            using (var reader = XmlReader.Create(memory))
            {
                var serializer = new NetDataContractSerializer();

                // Intentionally unsafe for demonstration:
                // deserializes a caller-controlled header value.
                return serializer.ReadObject(reader);
            }
        }

        private static void WriteText(HttpListenerContext context, int statusCode, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;

            using (var output = context.Response.OutputStream)
            {
                output.Write(bytes, 0, bytes.Length);
            }
        }
    }
}