# ADR-0003: IPC via Named Pipes para Comunicação Agent↔UI

## Status

Accepted

## Context

O TimeTrack Desktop tem dois processos separados:
- **AgentService**: Windows Service de background (tracking, sync)
- **DesktopHost**: Aplicação WinForms com WebView2 (UI React)

Precisávamos de comunicação entre esses processos para:
- UI enviar comandos (start/stop tracking, pause/resume)
- UI receber atualizações de estado
- Agent notificar UI sobre eventos (focus mode, idle)

Alternativas consideradas:
1. **Named Pipes** - IPC nativo do Windows, baixo overhead
2. **TCP Sockets** - Mais flexível mas requer portas
3. **HTTP/gRPC** - Overhead maior, mais complexo
4. **Shared Memory** - Complexo para mensagens

## Decision

Escolhemos **Named Pipes** para comunicação IPC:

### Arquitetura

```
┌─────────────────────┐         ┌─────────────────────┐
│   DesktopHost       │         │   AgentService      │
│   (WinForms +       │         │   (Windows Service) │
│    WebView2)        │         │                     │
│                     │         │                     │
│  ┌───────────────┐  │         │  ┌───────────────┐  │
│  │ IpcClient     │──┼─────────┼──│ IpcServer     │  │
│  │ (Named Pipe)  │  │  Pipe   │  │ (Named Pipe)  │  │
│  └───────────────┘  │         │  └───────────────┘  │
│                     │         │         │           │
│  ┌───────────────┐  │         │  ┌───────────────┐  │
│  │ WebViewBridge │  │         │  │ MessageRouter │  │
│  └───────────────┘  │         │  └───────────────┘  │
└─────────────────────┘         └─────────────────────┘
```

### Protocolo de Mensagens

```typescript
// Formato de mensagem (JSON)
interface IpcMessage {
  id: string;           // UUID para correlação
  type: 'command' | 'query' | 'event';
  action: string;       // ex: 'tracking/start', 'state/get'
  payload?: unknown;    // Dados da mensagem
  timestamp: number;    // Epoch ms
}

interface IpcResponse {
  id: string;           // Mesmo ID da request
  success: boolean;
  data?: unknown;
  error?: string;
}
```

### Pipe Name

```
\\.\pipe\TimeTrack.Agent
```

## Consequences

### Positivas

- **Baixo overhead**: Comunicação direta sem rede
- **Segurança**: Pipes locais, não expostos externamente
- **Simplicidade**: Nativo do Windows, sem dependências
- **Reliability**: Sistema operacional gerencia lifecycle

### Negativas

- **Windows-only**: Não portável para outras plataformas
- **Buffer limitado**: Mensagens grandes precisam de chunking
- **Debugging**: Mais difícil de inspecionar que HTTP

### Mitigações

- **Timeout adequado**: 5 segundos para operações normais
- **Retry logic**: Reconexão automática se pipe fechado
- **Logging detalhado**: Mensagens logadas para debugging
- **Validação**: Schema validation em todas as mensagens

## Implementation Notes

### Server (AgentService)

```csharp
public class NamedPipeIpcServer : IIpcServer
{
    private const string PipeName = "TimeTrack.Agent";

    public async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            await server.WaitForConnectionAsync(ct);
            _ = HandleClientAsync(server, ct);
        }
    }
}
```

### Client (DesktopHost)

```csharp
public class NamedPipeIpcClient : IIpcClient
{
    public async Task<IpcResponse> SendAsync(IpcMessage message, CancellationToken ct)
    {
        using var client = new NamedPipeClientStream(
            ".",
            "TimeTrack.Agent",
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await client.ConnectAsync(5000, ct);

        // Send and receive
        var json = JsonSerializer.Serialize(message);
        await WriteMessageAsync(client, json, ct);
        var response = await ReadMessageAsync(client, ct);

        return JsonSerializer.Deserialize<IpcResponse>(response);
    }
}
```

## References

- [Named Pipes - Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/system.io.pipes/)
- [Inter-Process Communication](https://docs.microsoft.com/en-us/windows/win32/ipc/interprocess-communications)
