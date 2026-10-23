using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyVoice.Infrastructure;

internal static class DiscordSignInTests
{
    public static void Run(Action<string,Action> test)
    {
        test("OAuth PKCE uses independent random verifiers and S256",()=>
        {
            var a=OAuthPkce.Create();var b=OAuthPkce.Create();
            var hash=Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(a.Verifier))).TrimEnd('=').Replace('+','-').Replace('/','_');
            if(a.Verifier.Length!=64||a.Verifier==b.Verifier||a.Challenge!=hash)throw new Exception("PKCE invalid");
        });
        test("Discord sign-in uses browser PKCE, correct callback and protected session",()=>SignIn().GetAwaiter().GetResult());
        test("OAuth callback rejects another state, host and duplicate code",()=>Callback().GetAwaiter().GetResult());
        test("OAuth callback cancellation closes wait without credentials",()=>
        {
            using var callback=new OAuthLoopback();using var cancel=new CancellationTokenSource();cancel.Cancel();
            try{callback.Receive(cancel.Token).GetAwaiter().GetResult();throw new Exception("Not cancelled");}catch(OperationCanceledException){}
        });
    }
    private static async Task SignIn()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string root=Path.Combine(Path.GetTempPath(),"MyVoice-OAuth-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"config"));
        try
        {
            var handler=new AuthHandler();using var client=new CommunityClient(new("https://test.supabase.co","sb_publishable_test"),root,handler);
            Task? browser=null;
            await client.SignInWithDiscord(timeout.Token,uri=>
            {
                var query=Query(uri.Query[1..]);
                if(uri.Host!="test.supabase.co"||query["provider"]!="discord"||query["code_challenge_method"]!="s256")throw new Exception("Wrong provider");
                handler.Challenge=query["code_challenge"];
                var callback=new Uri(query["redirect_to"]);
                browser=Request(callback,"?code=one-use-code",timeout.Token);
            });
            await browser!;
            if(client.Session?.UserId!="test-user"||!handler.Exchanged)throw new Exception("Session not established");
            if(File.ReadAllText(Path.Combine(root,"config","community.session")).Contains("private-access-token"))throw new Exception("Unprotected session");
            if(new SessionVault(root).Load()?.UserId!="test-user")throw new Exception("Session did not survive restart");
        }
        finally{Directory.Delete(root,true);}
    }
    private static async Task Callback()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));using var callback=new OAuthLoopback();
        var receive=callback.Receive(timeout.Token);
        foreach(var target in new[]{new Uri(callback.Redirect.GetLeftPart(UriPartial.Authority)+"/wrong"),callback.Redirect})
        {
            var result=await Request(target,target==callback.Redirect?"?code=a&code=b":"?code=a",timeout.Token);
            if(!result.StartsWith("HTTP/1.1 400"))throw new Exception("Invalid callback accepted");
        }
        var wrongHost=await Request(callback.Redirect,"?code=a",timeout.Token,"evil.invalid");
        if(!wrongHost.StartsWith("HTTP/1.1 400"))throw new Exception("Unexpected host accepted");
        await Request(callback.Redirect,"?code=valid",timeout.Token);
        if(await receive!="valid")throw new Exception("Wrong callback code");
    }
    private static async Task<string> Request(Uri uri,string query,CancellationToken token,string? host=null)
    {
        using var socket=new TcpClient();await socket.ConnectAsync(IPAddress.Loopback,uri.Port,token);using var stream=socket.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {uri.AbsolutePath}{query} HTTP/1.1\r\nHost: {host??uri.Authority}\r\n\r\n"),token);
        using var reader=new StreamReader(stream);return await reader.ReadToEndAsync(token);
    }
    private static Dictionary<string,string> Query(string query)=>query.Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1]));
    private sealed class AuthHandler:HttpMessageHandler
    {
        public string Challenge="";public bool Exchanged;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.RequestUri!.AbsolutePath=="/auth/v1/settings")return new(HttpStatusCode.OK){Content=new StringContent("{\"external\":{\"discord\":true}}")};
            if(request.RequestUri.PathAndQuery!="/auth/v1/token?grant_type=pkce"||request.Headers.Authorization!=null)throw new Exception("Unexpected authentication request");
            using var data=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            string verifier=data.RootElement.GetProperty("code_verifier").GetString()!;
            string hash=Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+','-').Replace('/','_');
            if(hash!=Challenge||data.RootElement.GetProperty("auth_code").GetString()!="one-use-code")throw new Exception("PKCE mismatch");
            Exchanged=true;return new(HttpStatusCode.OK){Content=new StringContent("{\"access_token\":\"private-access-token\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"user\":{\"id\":\"test-user\"}}")};
        }
    }
}
