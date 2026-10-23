using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MyVoice.Infrastructure;

internal static class DiscordPresenceTests
{
    public static void Run(Action<string,Action> test)
    {
        test("Discord is opt-in and never connects while disabled",()=>
        {
            int attempts=0;
            using var service=new DiscordPresence(_=>{Interlocked.Increment(ref attempts);throw new IOException();});
            service.SetEnabled(false);
            if(attempts!=0||service.Status!=DiscordPresenceStatus.Off||new MyVoice.Core.ProductState().DiscordPresence)throw new Exception("Default privacy failed");
        });
        test("Discord IPC handshake, privacy, ping and explicit activity removal",()=>Exchange().GetAwaiter().GetResult());
        test("Discord rejects oversized and truncated frames",()=>
        {
            foreach(int length in new[]{-1,65537,4})
            {
                byte[] header=new byte[8];BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4),length);
                using var input=new MemoryStream(header);
                try{DiscordPresence.Read(input,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Malformed frame accepted");}catch(IOException){}
            }
        });
    }
    private static async Task Exchange()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));var token=timeout.Token;
        string name="myvoice-test-"+Guid.NewGuid().ToString("N");
        using var server=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
        using var service=new DiscordPresence(async ct=>{var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);await client.ConnectAsync(ct);return client;});
        var acknowledged=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StatusChanged+=s=>{if(s==DiscordPresenceStatus.Connected)acknowledged.TrySetResult();};
        service.SetEnabled(true);await server.WaitForConnectionAsync(token);
        var handshake=await DiscordPresence.Read(server,token);
        using(var json=JsonDocument.Parse(handshake.Payload))
            if(handshake.Opcode!=0||json.RootElement.GetProperty("client_id").GetString()!=DiscordPresence.ApplicationId)throw new Exception("Wrong application");
        await DiscordPresence.Write(server,1,Encoding.UTF8.GetBytes("{\"evt\":\"READY\"}"),token);
        var activity=await DiscordPresence.Read(server,token);
        using(var json=JsonDocument.Parse(activity.Payload))
        {
            var value=json.RootElement.GetProperty("args").GetProperty("activity");
            if(value.EnumerateObject().Count()!=2||value.GetProperty("details").GetString()!="MyVoice · Studio audio")throw new Exception("Unexpected private activity data");
        }
        await DiscordPresence.Write(server,1,Encoding.UTF8.GetBytes("{\"cmd\":\"SET_ACTIVITY\",\"evt\":null}"),token);
        await acknowledged.Task.WaitAsync(token);
        await DiscordPresence.Write(server,3,Encoding.UTF8.GetBytes("ping-test"),token);
        var pong=await DiscordPresence.Read(server,token);
        if(pong.Opcode!=4||Encoding.UTF8.GetString(pong.Payload)!="ping-test")throw new Exception("Ping failed");
        service.SetEnabled(false);
        var clear=await DiscordPresence.Read(server,token);
        using(var json=JsonDocument.Parse(clear.Payload))if(json.RootElement.GetProperty("args").GetProperty("activity").ValueKind!=JsonValueKind.Null)throw new Exception("Activity was not cleared");
        await service.Stopped.WaitAsync(token);
        if(service.Status!=DiscordPresenceStatus.Off)throw new Exception("Presence remained active");
    }
}
