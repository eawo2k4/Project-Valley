using System;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Box86_WinUI.Models
{
    public enum ManagerMessage
    {
        RequestStatus,
        Pause,
        CtrlAltDel,
        ShowSettings,
        ResetVM,
        RequestShutdown,
        ForceShutdown,
        GlobalConfigurationChanged,
        UnknownMessage
    }

    public enum ClientMessage
    {
        Status,
        WindowBlocked,
        WindowUnblocked,
        RunningStateChanged,
        ConfigurationChanged,
        WinIdMessage,
        GlobalConfigurationChanged,
        UnknownMessage
    }

    public enum RunningState
    {
        Running = 0,
        Paused = 1,
        RunningWaiting = 2,
        PausedWaiting = 3,
        Unknown = 4
    }

    public class VMManagerProtocolSession : IDisposable
    {
        private readonly NamedPipeServerStream _pipeServer;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;

        public event Action<ClientMessage, JsonObject?>? MessageReceived;
        public event Action<RunningState>? StateChanged;
        public event Action<IntPtr>? WindowHandleReceived;

        public string PipeName { get; }

        public VMManagerProtocolSession(string pipeName)
        {
            PipeName = pipeName;
            _pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _readLoopTask = Task.Run(async () => await RunServerLoopAsync(_cts.Token));
        }

        private async Task RunServerLoopAsync(CancellationToken ct)
        {
            try
            {
                await _pipeServer.WaitForConnectionAsync(ct);

                byte[] buffer = new byte[4096];
                var sb = new StringBuilder();

                while (!ct.IsCancellationRequested && _pipeServer.IsConnected)
                {
                    int bytesRead = await _pipeServer.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead <= 0) break;

                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    sb.Append(chunk);

                    ProcessCompleteJsonMessages(sb);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC Server] Exception: {ex.Message}");
            }
        }

        private void ProcessCompleteJsonMessages(StringBuilder sb)
        {
            // The protocol sends individual JSON strings
            string current = sb.ToString();
            int openBraces = 0;
            int startIdx = -1;

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == '{')
                {
                    if (openBraces == 0) startIdx = i;
                    openBraces++;
                }
                else if (current[i] == '}')
                {
                    openBraces--;
                    if (openBraces == 0 && startIdx >= 0)
                    {
                        string jsonStr = current.Substring(startIdx, i - startIdx + 1);
                        HandleIncomingJson(jsonStr);
                        sb.Remove(0, i + 1);
                        current = sb.ToString();
                        i = -1;
                        startIdx = -1;
                    }
                }
            }
        }

        private void HandleIncomingJson(string jsonString)
        {
            try
            {
                var doc = JsonNode.Parse(jsonString)?.AsObject();
                if (doc == null) return;

                string? msgTypeStr = doc["message"]?.ToString();
                if (msgTypeStr == null) return;

                if (!Enum.TryParse<ClientMessage>(msgTypeStr, true, out var clientMsg))
                    clientMsg = ClientMessage.UnknownMessage;

                var paramsObj = doc["params"]?.AsObject();

                if (clientMsg == ClientMessage.RunningStateChanged && paramsObj != null)
                {
                    int stateInt = paramsObj["state"]?.GetValue<int>() ?? 0;
                    StateChanged?.Invoke((RunningState)stateInt);
                }
                else if (clientMsg == ClientMessage.WinIdMessage)
                {
                    long winId = 0;
                    if (paramsObj != null)
                    {
                        winId = paramsObj["winId"]?.GetValue<long>() ??
                                paramsObj["params"]?.GetValue<long>() ??
                                paramsObj["id"]?.GetValue<long>() ?? 0;
                    }
                    else if (doc["params"] is JsonValue jval && jval.TryGetValue<long>(out var scalarVal))
                    {
                        winId = scalarVal;
                    }

                    if (winId != 0)
                    {
                        WindowHandleReceived?.Invoke(new IntPtr(winId));
                    }
                }

                MessageReceived?.Invoke(clientMsg, paramsObj);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC Protocol] Failed to parse message: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(ManagerMessage message, object? parameters = null)
        {
            if (!_pipeServer.IsConnected) return;

            var obj = new JsonObject
            {
                ["type"] = "Manager",
                ["message"] = message.ToString()
            };

            if (parameters != null)
            {
                obj["params"] = JsonSerializer.SerializeToNode(parameters);
            }

            string payload = obj.ToJsonString();
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _pipeServer.WriteAsync(bytes, 0, bytes.Length);
            await _pipeServer.FlushAsync();
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _pipeServer.Dispose();
        }
    }
}
