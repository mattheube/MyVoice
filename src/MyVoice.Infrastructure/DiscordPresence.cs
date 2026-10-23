using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace MyVoice.Infrastructure;

public enum DiscordPresenceStatus { Off, Waiting, Connected }

// Local Discord IPC only. No account token, microphone data, voice title or file path is sent.
public sealed class DiscordPresence : IDisposable
{
    public const string ApplicationId = "1558080529360887909";
    private readonly Func<CancellationToken, Task<Stream>> connect;
    private readonly object gate = new();
    private CancellationTokenSource? lifetime;
    private Task worker = Task.CompletedTask;
    private bool disposed;
    private DiscordPresenceStatus status;
    public event Action<DiscordPresenceStatus>? StatusChanged;
    public DiscordPresenceStatus Status => status;
    public DiscordPresence(Func<CancellationToken, Task<Stream>>? connector = null) => connect = connector ?? Connect;

    public void SetEnabled(bool enabled)
    {
        lock(gate)
        {
            if(disposed || enabled == (lifetime != null))return;
            lifetime?.Cancel();lifetime = null;
            if(!enabled){SetStatus(DiscordPresenceStatus.Off);return;}
            var next = new CancellationTokenSource();lifetime = next;
            var previous = worker;
            worker = Task.Run(async () =>
            {
                try{await previous.ConfigureAwait(false);}catch(OperationCanceledException){}
                try{await Run(next.Token).ConfigureAwait(false);}finally{next.Dispose();}
            });
        }
    }
    private void SetStatus(DiscordPresenceStatus value)
    {
        if(status == value)return;status = value;StatusChanged?.Invoke(value);
    }
    private async Task Run(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            SetStatus(DiscordPresenceStatus.Waiting);
            try
            {
                using var pipe = await connect(token).ConfigureAwait(false);
                try
                {
                    using(var handshake = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        handshake.CancelAfter(TimeSpan.FromSeconds(5));
                        await Write(pipe, 0, JsonSerializer.SerializeToUtf8Bytes(new{v=1,client_id=ApplicationId}), handshake.Token);
                        var ready = await Read(pipe, handshake.Token);
                        using var json = JsonDocument.Parse(ready.Payload);
                        if(ready.Opcode != 1 || !json.RootElement.TryGetProperty("evt",out var evt) || evt.GetString() != "READY")throw new IOException("Discord handshake declined.");
                        await Write(pipe, 1, Activity(false), handshake.Token);
                    }
                    while(!token.IsCancellationRequested)
                    {
                        var frame = await Read(pipe, token);
                        if(frame.Opcode == 2)throw new IOException("Discord disconnected.");
                        if(frame.Opcode == 3){await Write(pipe,4,frame.Payload,token);continue;}
                        if(frame.Opcode != 1)continue;
                        using var data = JsonDocument.Parse(frame.Payload);
                        if(data.RootElement.TryGetProperty("evt",out var evt) && evt.GetString()=="ERROR")throw new IOException("Discord declined activity.");
                        if(!token.IsCancellationRequested && data.RootElement.TryGetProperty("cmd",out var cmd) && cmd.GetString()=="SET_ACTIVITY")SetStatus(DiscordPresenceStatus.Connected);
                    }
                }
                finally
                {
                    // Clear the activity before releasing the connection, even when the user disables it.
                    using var clear = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    try{await Write(pipe,1,Activity(true),clear.Token);}catch(Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException){}
                }
            }
            catch(Exception e) when(e is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException)
            {if(token.IsCancellationRequested)break;}
            if(token.IsCancellationRequested)break;
            SetStatus(DiscordPresenceStatus.Waiting);
            try{await Task.Delay(TimeSpan.FromSeconds(30),token);}catch(OperationCanceledException){break;}
        }
    }
    private static byte[] Activity(bool clear) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        cmd="SET_ACTIVITY",nonce=Guid.NewGuid().ToString("N"),
        args=new{pid=Environment.ProcessId,activity=clear?null:new{details="MyVoice · Studio audio",type=0}}
    });
    private static async Task<Stream> Connect(CancellationToken token)
    {
        for(int index=0;index<10;index++)
        {
            var pipe=new NamedPipeClientStream(".","discord-ipc-"+index,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            try{await pipe.ConnectAsync(150,token);return pipe;}
            catch(Exception e) when(e is IOException or TimeoutException){pipe.Dispose();}
            catch{pipe.Dispose();throw;}
        }
        throw new IOException("Discord is not running.");
    }
    public static async Task Write(Stream stream,int opcode,ReadOnlyMemory<byte> payload,CancellationToken token)
    {
        if(payload.Length>65536)throw new InvalidDataException("Discord frame too large.");
        byte[] header=new byte[8];BinaryPrimitives.WriteInt32LittleEndian(header,opcode);BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4),payload.Length);
        await stream.WriteAsync(header,token);await stream.WriteAsync(payload,token);await stream.FlushAsync(token);
    }
    public static async Task<(int Opcode,byte[] Payload)> Read(Stream stream,CancellationToken token)
    {
        byte[] header=new byte[8];await stream.ReadExactlyAsync(header,token);
        int size=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if(size<0||size>65536)throw new IOException("Invalid Discord frame length.");
        byte[] payload=new byte[size];await stream.ReadExactlyAsync(payload,token);return(BinaryPrimitives.ReadInt32LittleEndian(header),payload);
    }
    public Task Stopped => worker;
    public void Dispose(){lock(gate){if(disposed)return;SetEnabled(false);disposed=true;}}
}
