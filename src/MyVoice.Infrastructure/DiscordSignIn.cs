using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace MyVoice.Infrastructure;

public sealed partial class CommunityClient
{
    public async Task<bool> DiscordSignInAvailable(CancellationToken token=default)
    {
        var settings=await Send("/auth/v1/settings",HttpMethod.Get,authenticated:false,cancellationToken:token);
        return settings.TryGetProperty("external",out var external)&&external.TryGetProperty("discord",out var enabled)&&enabled.ValueKind==System.Text.Json.JsonValueKind.True;
    }
    public async Task SignInWithDiscord(CancellationToken token,Action<Uri>? openBrowser=null)
    {
        if(!await DiscordSignInAvailable(token))throw new InvalidOperationException("La connexion Discord n’est pas encore activée sur le serveur MyVoice.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var callback=new OAuthLoopback();
        var pkce=OAuthPkce.Create();
        var authorize=new Uri(config.Url.TrimEnd('/')+"/auth/v1/authorize?provider=discord&redirect_to="+Uri.EscapeDataString(callback.Redirect.AbsoluteUri)+"&code_challenge="+pkce.Challenge+"&code_challenge_method=s256");
        (openBrowser??(uri=>Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true})))(authorize);
        string code=await callback.Receive(timeout.Token);
        var result=await Send("/auth/v1/token?grant_type=pkce",HttpMethod.Post,new{auth_code=code,code_verifier=pkce.Verifier},authenticated:false,cancellationToken:timeout.Token);
        if(!result.TryGetProperty("access_token",out _))throw new InvalidDataException("Connexion Discord incomplète. Réessayez.");
        SaveSession(result);
    }
}

public sealed record OAuthPkce(string Verifier,string Challenge)
{
    public static OAuthPkce Create(){string verifier=Encode(RandomNumberGenerator.GetBytes(48));return new(verifier,Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));}
    private static string Encode(byte[] value)=>Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');
}

// Binds only a random loopback port and accepts one bounded, nonce-bound GET callback.
// No tokens travel through the browser; only a single-use PKCE code does.
public sealed class OAuthLoopback : IDisposable
{
    private readonly TcpListener listener=new(IPAddress.Loopback,0);
    public Uri Redirect{get;}
    public OAuthLoopback()
    {
        listener.Start(4);
        Redirect=new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/myvoice-discord/{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}");
    }
    public async Task<string> Receive(CancellationToken token)
    {
        while(true)
        {
            using var client=await listener.AcceptTcpClientAsync(token);using var stream=client.GetStream();
            using var requestTimeout=CancellationTokenSource.CreateLinkedTokenSource(token);requestTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            string? code=null;bool denied=false;
            try
            {
                var request=new List<byte>();byte[] one=new byte[1];
                while(request.Count<8192)
                {
                    if(await stream.ReadAsync(one,requestTimeout.Token)==0)break;request.Add(one[0]);
                    int n=request.Count;if(n>=4&&request[n-4]==13&&request[n-3]==10&&request[n-2]==13&&request[n-1]==10)break;
                }
                string text=Encoding.ASCII.GetString(request.ToArray());
                if(!text.EndsWith("\r\n\r\n",StringComparison.Ordinal))throw new IOException("Incomplete callback.");
                var lines=text.Split("\r\n");var first=lines[0].Split(' ');
                var hosts=lines.Skip(1).Where(l=>l.StartsWith("Host:",StringComparison.OrdinalIgnoreCase)).ToArray();
                if(first.Length==3&&first[0]=="GET"&&first[1].StartsWith(Redirect.AbsolutePath+"?",StringComparison.Ordinal)&&hosts.Length==1&&hosts[0][5..].Trim()==Redirect.Authority)
                {
                    var parameters=first[1][(first[1].IndexOf('?')+1)..].Split('&').Select(p=>p.Split('=',2)).ToArray();
                    denied=parameters.Any(p=>p[0] is "error" or "error_code");
                    var codes=parameters.Where(p=>p[0]=="code"&&p.Length==2).ToArray();
                    if(codes.Length==1){var value=Uri.UnescapeDataString(codes[0][1]);if(value.Length is >0 and <=2048&&!value.Any(char.IsControl))code=value;}
                }
                string body=code!=null?"Retournez dans MyVoice pour terminer la connexion. Vous pouvez fermer cette page.":denied?"Connexion annulee. Vous pouvez fermer cette page.":"Callback invalide.";
                byte[] bytes=Encoding.UTF8.GetBytes(body);
                string headers=$"HTTP/1.1 {(code!=null||denied?"200 OK":"400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(headers),requestTimeout.Token);await stream.WriteAsync(bytes,requestTimeout.Token);
            }
            catch(Exception e) when(e is IOException or OperationCanceledException){token.ThrowIfCancellationRequested();continue;}
            if(denied)throw new InvalidOperationException("Connexion Discord annulée.");
            if(code!=null)return code;
        }
    }
    public void Dispose()=>listener.Stop();
}
