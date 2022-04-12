using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Refit;

namespace Tbc.Core.Http;

public class HttpServer<THandler>
{
    private readonly Action<string> _log;
    private readonly HttpListener _listener;

    public Dictionary<string, (Type ParameterType, Func<object, Task<object>>, Type ReturnType)>
        _handlerOperations = new();

    public HttpServer (int listenPort, THandler handler, Action<string>? log = default)
    {
        _log = log ?? Console.WriteLine;

        _listener = new HttpListener { Prefixes = { $"http://+:{listenPort}/" } };

        SetHandlerOperations(handler);
    }

    public Task Run()
    {
        _listener.Start();

#pragma warning disable CS4014
        Task.Run(async () => await RunRequestLoop())
           .ContinueWith(t =>
#pragma warning restore CS4014
            {
                Console.WriteLine("Request loop terminated:");
                Console.WriteLine(t);
            });

        return Task.CompletedTask;
    }

    private async Task RunRequestLoop()
    {
        while (true)
        {
            var requestContext = await _listener.GetContextAsync().ConfigureAwait(false);

#pragma warning disable CS4014
            Task.Run(async () =>
#pragma warning restore CS4014
            {
                var (req, resp) = (requestContext.Request, requestContext.Response);

                Console.WriteLine($"{req.HttpMethod} {req.Url}");

                try
                {
                    var ret = await HandleRequest(req.Url.AbsolutePath, req.InputStream);
                    await Write(ret, resp).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log(ex.ToString());
                    resp.StatusCode = 500;
                    var errorBytes = Encoding.UTF8.GetBytes(ex.ToString());
                    await resp.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length).ConfigureAwait(false);
                    resp.Close();
                }
            });
        }
    }

    private async Task<object> HandleRequest (string path, Stream inputStream)
    {
        var (type, action, output) = _handlerOperations[path];

        using var sr = new StreamReader(inputStream);
        var json = await sr.ReadToEndAsync();
        var content = JsonSerializer.Deserialize(json, type, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var ret = await action(content);

        return ret;
    }

    private static async Task Write(object ret, HttpListenerResponse resp)
    {
        var json = JsonSerializer.Serialize(ret);
        var buffer = Encoding.UTF8.GetBytes(json);
        resp.StatusCode = 200;

        using var ms = new MemoryStream();
        using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
            zip.Write(buffer, 0, buffer.Length);

        buffer = ms.ToArray();

        resp.AddHeader("Content-Encoding", "gzip");
        resp.ContentLength64 = buffer.Length;
        await resp.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        await resp.OutputStream.FlushAsync();

        resp.Close();
    }

    private void SetHandlerOperations(THandler handler)
    {
        _handlerOperations = typeof(THandler).GetMethods().Concat(handler.GetType().GetMethods())
           .Select(x => (x.GetCustomAttribute<PostAttribute>(), x))
           .Where(x => x.Item1 != null)
           .ToDictionary(x => x.Item1.Path, x => (x.x.GetParameters()[0].ParameterType,
                new Func<object, Task<object>>(async y =>
                {
                    var t = (Task)x.x.Invoke(handler, new[] { y });
                    await t;
                    return t.GetType().GetProperty("Result")!.GetValue(t);
                }), x.x.ReturnType));

        foreach (var operation in _handlerOperations)
            _log($"{operation.Key}: {operation.Value.Item1.Name} -> {operation.Value.Item3}");
    }
}

