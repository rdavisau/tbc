using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Tbc.Core.Extensions;
using Tbc.Core.Models;
using Tbc.Core.Socket.Abstractions;
using Tbc.Core.Socket.Extensions;
using Tbc.Core.Socket.Models;
using Tbc.Core.Socket.Serialization;
using Tbc.Core.Socket.Serialization.Serializers;

namespace Tbc.Core.Socket;

public class SocketServer<TProtocol> : IRemoteEndpoint
{
    public string Identifier { get; }
    public TcpClient Socket { get; private set; }
    private Stream Stream => Socket.GetStream();
    public object Handler { get; }

    public Dictionary<int, Type> Protocol = new();
    public Dictionary<Type, SocketHandlerOperation> HandlerOperations = new();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests = new();
    private readonly Func<Task>? _onDisconnect;
    private readonly Action<string> _log;
    private bool _finished;

    private SocketSerializationFormat _serializationFormat = SocketSerializationFormat.Json;
    public ISocketSerializer Serializer { get; set; }

    public Action<object>? OnReceived { get; set; }

    public SocketServer (TcpClient socket, object handler, string identifier,
        Action<string>? log = null,
        Func<Task>? onDisconnect = null)
    {
        _log = log ?? Console.WriteLine;
        _log = x => { Console.Write($"{Identifier}: "); log?.Invoke(x); };
        _onDisconnect = onDisconnect;

        Socket = socket;
        Handler = handler;
        Identifier = identifier;

        Socket.ReceiveBufferSize = 1024 * 1024 * 5;
        Socket.SendBufferSize = 1024 * 1024 * 5;

        Serializer = _serializationFormat switch
        {
            SocketSerializationFormat.Json => new ClearTextSystemTextJsonSocketSerializer(),
            SocketSerializationFormat.CompressedJson => new SystemTextJsonSocketSerializer(),
            SocketSerializationFormat.MessagePack => new MessagePackSocketSerializer(),
            _ => throw new ArgumentOutOfRangeException()
        };

        if (Handler is ISendToRemote str)
            str.Remote = this;

        SetProtocol();
        SetHandlerOperations();
    }

    public Task Run(CancellationToken ct = default)
    {
#pragma warning disable CS4014
        Task.Run(async () => await RunRequestLoop(ct))
           .ContinueWith(t =>
#pragma warning restore CS4014
            {
                _log("Request loop terminated: ");
                _log(t.Exception?.ToString() ?? "no exception");
                _finished = true;
            });

        return Task.CompletedTask;
    }

    private async Task RunRequestLoop(CancellationToken ct = default)
    {
        while (true)
        {
            var receiveResult = await ReceiveAndHandle(ct);
            _log($"receive result: {receiveResult}");

            if (receiveResult.Outcome == ReceiveResultOutcome.Disconnect)
            {
                if (_onDisconnect is { } od)
                    await od.Invoke();

                break;
            }
        }
    }

    private async Task<ReceiveResult> ReceiveAndHandle(CancellationToken ct = default)
    {
        var receiveResult = await Receive(ct);

        OnReceived?.Invoke(receiveResult);

        if (receiveResult.Outcome != ReceiveResultOutcome.Success || receiveResult.Message is null)
            return receiveResult;

        var requestIdentifier = receiveResult.Message.RequestIdentifier;
        var data = receiveResult.Message.Payload;

        if (receiveResult.Message.Kind is SocketMessageKind.Request)
        {
            // invoke handler and return response
            var receivedType = data.GetType();
            var operation = HandlerOperations.GetValueOrDefault(receivedType);
            if (operation is null)
            {
                _log($"WARN: No handler for message: ({receivedType.Name}): {data}");
                return receiveResult with { Outcome = ReceiveResultOutcome.RequestNotHandled };
            }

            var result = await operation.Handler(data);
            _log($"Result for operation {operation.Name}: {result}");

            var resultType = result.GetType();
            var responseMessageEnvelopeType = typeof(SocketResponse<>).MakeGenericType(resultType);
            var response = (ISocketMessage) Activator.CreateInstance(responseMessageEnvelopeType, requestIdentifier, result);

            await SendResponse(receiveResult.Message, response, ct);

            return receiveResult with { Response = response };
        }
        else
        {
            // route to pending request
            if (!_pendingRequests.TryRemove(requestIdentifier, out var matchingCompletionSource))
            {
                _log($"Received result with identifier {requestIdentifier} ({data}) but there was no pending request");

                return receiveResult with { Outcome = ReceiveResultOutcome.WaywardMessage };
            }

            matchingCompletionSource.SetResult(receiveResult);

            return receiveResult;
        }
    }

    public async Task<TResponse> SendRequest<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (_finished)
        {
            _log("ignoring attempt to send message on finished socket server, please find the caller " +
                 "who did this and prevent them from doing this >:)");

            return default!;
        }

        var responseMessageId =
            Protocol.Where(x => x.Value == request.GetType()).Select(x => x.Key).FirstOrDefault();

        var identifier = Guid.NewGuid().ToString();
        var requestEnvelope = new SocketRequest<TRequest> { RequestIdentifier = identifier, Payload = request };
        var requestData = Serializer.Serialize(requestEnvelope);
        var requestLength = requestData.Length;

        var final = Enumerable
               .Concat(BitConverter.GetBytes((int)(SocketMessageKind.Request)), BitConverter.GetBytes(responseMessageId))
               .Concat(BitConverter.GetBytes(requestLength)).Concat(requestData)
           .ToArray();

        var tcs = new TaskCompletionSource<object>();
        _pendingRequests.AddOrUpdate(identifier, _ => tcs, (_, _) => tcs);

        _log($"Sending {request.GetType().Name} ({final.Length:N0} bytes in envelope): {requestData}");

        var stream = Stream;

        await stream.WriteAsync(final, 0, final.Length, ct);
        await stream.FlushAsync(ct);

        var response = (ReceiveResult) await tcs.Task;

        if (response.Message is not SocketResponse<TResponse> expectedResponse)
        {
            _log($"Expected response of type {typeof(TResponse).Name} but received {response.GetType().Name}");
            throw new Exception("Unexpected response to request");
        }

        return expectedResponse.Data;
    }

    public async Task SendResponse(ISocketMessage request, ISocketMessage response, CancellationToken ct = default)
    {
        var responseType = response.Payload.GetType();

        var responseMessageId =
            Protocol.Where(x => x.Value == responseType).Select(x => x.Key).FirstOrDefault();

        var requestEnvelope = response;
        var requestData = Serializer.Serialize(requestEnvelope);
        var requestLength = requestData.Length;

        var final = Enumerable
           .Concat(BitConverter.GetBytes((int)(SocketMessageKind.Response)), BitConverter.GetBytes(responseMessageId))
           .Concat(BitConverter.GetBytes(requestLength)).Concat(requestData)
           .ToArray();

        _log($"Sending {responseType.Name} ({final.Length:N0} bytes in envelope): {requestData}");

        var stream = Stream;

        await stream.WriteAsync(final, 0, final.Length, ct);
        await stream.FlushAsync(ct);
    }

    private async Task<ReceiveResult> Receive(CancellationToken ct)
    {
        // protocol is
        // socket message kind
        // int message id
        // int message length
        // length bytes of data
        var incomingMessageLength = sizeof(int) + sizeof(int) + sizeof(int);

        var typeBuf = new byte[incomingMessageLength];
        var stream = Stream;

        var len = await stream.ReadAsync(typeBuf, 0, incomingMessageLength, ct);
        if (len < incomingMessageLength)
        {
            return new ReceiveResult { Outcome = ReceiveResultOutcome.Disconnect };
        }

        var socketMessageKind = (SocketMessageKind)BitConverter.ToInt32(typeBuf, 0);
        var messageId = BitConverter.ToInt32(typeBuf, 4);
        var messageLength = BitConverter.ToInt32(typeBuf, 8);

        var buffer = await stream.ReadExactly(messageLength);

        var incomingType = Protocol.GetValueOrDefault(messageId);
        if (incomingType is null)
        {
            _log($"Received unrecognised message with id: {messageId}");
            return new ReceiveResult { Outcome = ReceiveResultOutcome.ProtocolNotRecognised };
        }

        var envelopedIncomingType =
            socketMessageKind switch
            {
                SocketMessageKind.Request => typeof(SocketRequest<>).MakeGenericType(incomingType),
                SocketMessageKind.Response => typeof(SocketResponse<>).MakeGenericType(incomingType),
                _ => throw new ArgumentOutOfRangeException()
            };

        var message = (ISocketMessage)
            Serializer.Deserialize(envelopedIncomingType, buffer);

        return new ReceiveResult
        {
            Kind = socketMessageKind,
            Outcome = ReceiveResultOutcome.Success,
            Message = message
        };
    }

    private void SetProtocol()
    {
        Protocol = GetMethodsInInterfaceHierarchy(typeof(TProtocol))
           .SelectMany(x => new [] { x.GetParameters()[0].ParameterType, x.ReturnType.GetGenericArguments()[0] })
           .GroupBy(x => x)
           .Select(x => x.First())
           .OrderBy(x => x.Name)
           .Select((x,i) => new { Index = i + 1, Type = x })
           .ToDictionary(x => x.Index, x => x.Type);

        _log("Protocol:");
        foreach (var model in Protocol)
            _log($"\t[{model.Key}]: {model.Value}");
    }

    // assume all handlers
    // have one parameter
    // return Task<return type>
    private void SetHandlerOperations()
    {
        HandlerOperations = Handler.GetType().GetMethods().Concat(Handler.GetType().GetMethods())
           .Where(x => x.GetParameters().Any() && Protocol.ContainsValue(x.GetParameters()[0].ParameterType))
           .GroupBy(x => x.GetParameters()[0].ParameterType)
           .Select(x => x.First())
           .Select(x =>
                new SocketHandlerOperation(x.Name, x.GetParameters()[0].ParameterType, x.ReturnType.GetGenericArguments()[0],
                    async y =>
                    {
                        var t = (Task)x.Invoke(Handler, new[] { y });
                        await t; return t.GetType().GetProperty("Result")!.GetValue(t);
                    }))
           .ToDictionary(x => x.RequestType);

        _log("Handler Operations:");
        foreach (var operation in HandlerOperations.Values)
            _log($"\t{operation.Name}: {operation.RequestType} -> {operation.ResponseType}");
    }

    static IEnumerable<MethodInfo> GetMethodsInInterfaceHierarchy(Type type) {
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                               BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
            yield return method;
        }
        if (type.IsInterface) {
            foreach (var iface in type.GetInterfaces()) {
                foreach (var method in GetMethodsInInterfaceHierarchy(iface)) {
                    yield return method;
                }
            }
        }
    }
}
