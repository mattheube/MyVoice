using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace MyVoice.Infrastructure;

public sealed record CommunityConfiguration(string Url="",string PublishableKey="")
{
    public bool Ready=>Uri.TryCreate(Url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host.EndsWith(".supabase.co",StringComparison.OrdinalIgnoreCase)&&PublishableKey.StartsWith("sb_publishable_",StringComparison.Ordinal);
    public static CommunityConfiguration Load(string file)=>File.Exists(file)?JsonSerializer.Deserialize<CommunityConfiguration>(File.ReadAllText(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new():new();
}
public sealed record AccountSession(string AccessToken,string RefreshToken,string UserId,DateTimeOffset ExpiresAt);
public sealed class SessionVault(string root)
{
    private string FileName=>Path.Combine(root,"config","community.session");
    public AccountSession? Load()
    {
        if(!File.Exists(FileName))return null;
        try{var bytes=ProtectedData.Unprotect(File.ReadAllBytes(FileName),null,DataProtectionScope.CurrentUser);try{return JsonSerializer.Deserialize<AccountSession>(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}}catch(Exception e) when(e is CryptographicException or JsonException or IOException){return null;}
    }
    public void Save(AccountSession session){var bytes=JsonSerializer.SerializeToUtf8Bytes(session);try{File.WriteAllBytes(FileName,ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser));}finally{CryptographicOperations.ZeroMemory(bytes);}}
    public void Clear(){if(File.Exists(FileName))File.Delete(FileName);}
}
public static class AccountRules
{
    private static readonly string[] Reserved=["myvoice","admin","administrator","support","moderator","official","system"];
    public static string Username(string value)
    {var name=value.Trim().TrimStart('@').ToLowerInvariant();if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-z0-9_]{3,24}$")||Reserved.Contains(name))throw new InvalidDataException("Identifiant indisponible : 3–24 lettres, chiffres ou _. ");return name;}
    public static int PasswordScore(string password)
    {
        if(password.Length<10||password.Distinct().Count()<5||new[]{"password","motdepasse","1234567890","qwerty","azerty"}.Any(p=>password.Contains(p,StringComparison.OrdinalIgnoreCase)))return 0;
        return password.Length>=20?3:password.Length>=14?2:1;
    }
}
public sealed class CommunityClient : IDisposable
{
    private readonly CommunityConfiguration config;private readonly SessionVault vault;
    private readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(12)};
    private readonly SemaphoreSlim refresh=new(1,1);
    public bool Configured=>config.Ready;
    public AccountSession? Session {get;private set;}
    public CommunityClient(CommunityConfiguration config,string root){this.config=config;vault=new(root);Session=vault.Load();}
    private async Task<JsonElement> Send(string path,HttpMethod method,object? body=null,bool authenticated=true)
    {
        if(!Configured)throw new InvalidOperationException("Community hors ligne : service non connecté.");
        using var req=new HttpRequestMessage(method,config.Url.TrimEnd('/')+path);req.Headers.Add("apikey",config.PublishableKey);
        if(authenticated&&Session!=null)req.Headers.Authorization=new("Bearer",Session.AccessToken);
        if(body!=null)req.Content=JsonContent.Create(body);
        using var res=await http.SendAsync(req);
        if(!res.IsSuccessStatusCode)throw new InvalidOperationException(res.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden?"Connexion ou autorisation requise.":"Action refusée par le service. Vérifiez les informations et réessayez.");
        var raw=await res.Content.ReadAsStringAsync();return string.IsNullOrWhiteSpace(raw)?JsonSerializer.SerializeToElement(new{}):JsonDocument.Parse(raw).RootElement.Clone();
    }
    private void SaveSession(JsonElement value)
    {
        if(!value.TryGetProperty("access_token",out var access))return;
        Session=new(access.GetString()!,value.GetProperty("refresh_token").GetString()!,value.GetProperty("user").GetProperty("id").GetString()!,DateTimeOffset.UtcNow.AddSeconds(value.GetProperty("expires_in").GetInt32()));vault.Save(Session);
    }
    public async Task Register(string email,string username,string display,string password,string confirmation)
    {
        if(password!=confirmation)throw new InvalidDataException("Les mots de passe ne correspondent pas.");
        if(AccountRules.PasswordScore(password)==0)throw new InvalidDataException("Choisissez une phrase de passe d’au moins 10 caractères, difficile à deviner.");
        var handle=AccountRules.Username(username);if(string.IsNullOrWhiteSpace(display)||display.Length>60)throw new InvalidDataException("Nom affiché requis, 60 caractères maximum.");
        var result=await Send("/auth/v1/signup",HttpMethod.Post,new{email,password,data=new{username=handle,display_name=display}},false);SaveSession(result);
    }
    public async Task Login(string email,string password)=>SaveSession(await Send("/auth/v1/token?grant_type=password",HttpMethod.Post,new{email,password},false));
    public Task Recover(string email)=>Send("/auth/v1/recover",HttpMethod.Post,new{email},false);
    public async Task Logout(){try{if(Session!=null)await Send("/auth/v1/logout",HttpMethod.Post);}finally{Session=null;vault.Clear();}}
    public async Task EnsureSession()
    {
        await refresh.WaitAsync();try{if(Session!=null&&Session.ExpiresAt<DateTimeOffset.UtcNow.AddMinutes(2))SaveSession(await Send("/auth/v1/token?grant_type=refresh_token",HttpMethod.Post,new{refresh_token=Session.RefreshToken},false));}finally{refresh.Release();}
    }
    public async Task<JsonElement> Rpc(string name,object data){await EnsureSession();return await Send("/rest/v1/rpc/"+name,HttpMethod.Post,data);}
    public async Task<JsonElement> Feed(string search,string kind,string sort)
    {
        await EnsureSession();var path="/rest/v1/content?select=*&status=eq.published&order="+(sort=="Featured"?"featured.desc,":"")+"created_at.desc&limit=60";
        if(kind!="all")path+="&kind=eq."+Uri.EscapeDataString(kind);
        if(!string.IsNullOrWhiteSpace(search))path+="&title=ilike."+Uri.EscapeDataString("*"+search.Replace("*","")+"*");
        return await Send(path,HttpMethod.Get);
    }
    public async Task UpdateProfile(string display,string bio,bool isPrivate)
    {
        await EnsureSession();if(Session==null)throw new InvalidOperationException("Connectez-vous.");
        await Send("/rest/v1/profiles?id=eq."+Session.UserId,HttpMethod.Patch,new{display_name=display,bio,@private=isPrivate});
    }
    public async Task<JsonElement> OwnProfile(){await EnsureSession();if(Session==null)throw new InvalidOperationException("Connectez-vous.");return await Send("/rest/v1/profiles?id=eq."+Session.UserId+"&select=username,display_name,bio,private",HttpMethod.Get);}
    public async Task<string> Download(string id,string folder,IProgress<double>? progress=null)
    {
        await EnsureSession();var data=await Send("/functions/v1/community-package?id="+Uri.EscapeDataString(id),HttpMethod.Get);
        var uri=new Uri(data.GetProperty("url").GetString()!);if(uri.Scheme!="https"||uri.Host!=new Uri(config.Url).Host)throw new InvalidDataException("Stockage non autorisé.");
        Directory.CreateDirectory(folder);var file=Path.Combine(folder,Guid.NewGuid()+".myvoice");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(4));
        using var res=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token);res.EnsureSuccessStatusCode();
        var total=data.GetProperty("size").GetInt64();if(total>CreationPackage.MaxArchive||total<=0)throw new InvalidDataException("Package trop volumineux.");
        try
        {
            await using(var output=File.Create(file)){await using var input=await res.Content.ReadAsStreamAsync(timeout.Token);var buffer=new byte[65536];long received=0;int count;while((count=await input.ReadAsync(buffer,timeout.Token))>0){received+=count;if(received>total)throw new InvalidDataException("Taille incohérente.");await output.WriteAsync(buffer.AsMemory(0,count),timeout.Token);progress?.Report(received*100d/total);}if(received!=total)throw new InvalidDataException("Téléchargement incomplet.");}
            await using var verify=File.OpenRead(file);var actual=Convert.ToHexString(await SHA256.HashDataAsync(verify,timeout.Token));if(!actual.Equals(data.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package altéré.");return file;
        }catch{if(File.Exists(file))File.Delete(file);throw;}
    }
    public async Task<string> Publish(string file,object metadata)
    {
        await EnsureSession();if(Session==null||!Configured)throw new InvalidOperationException("Connectez-vous pour publier.");
        CreationPackage.Inspect(file);using var req=new HttpRequestMessage(HttpMethod.Post,config.Url.TrimEnd('/')+"/functions/v1/community-package?metadata="+Uri.EscapeDataString(JsonSerializer.Serialize(metadata)));
        req.Headers.Add("apikey",config.PublishableKey);req.Headers.Authorization=new("Bearer",Session.AccessToken);req.Content=new StreamContent(File.OpenRead(file));req.Content.Headers.ContentType=new("application/zip");
        using var res=await http.SendAsync(req);if(!res.IsSuccessStatusCode)throw new InvalidOperationException("Publication refusée. Vérifiez votre email, les droits du contenu et réessayez.");var json=JsonDocument.Parse(await res.Content.ReadAsStringAsync());return json.RootElement.GetProperty("url").GetString()!;
    }
    public void Dispose(){http.Dispose();refresh.Dispose();}
}
