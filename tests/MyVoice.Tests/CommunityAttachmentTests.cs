using System.Net;
using System.Net.Http.Json;
using MyVoice.Infrastructure;

static class CommunityAttachmentTests
{
    public static void Run(Action<string,Action> test)
    {
        test("Community covers reuse bounded disk cache and preview has a separate path",()=>Exercise().GetAwaiter().GetResult());
    }
    private static async Task Exercise()
    {
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-Covers-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try{
            var handler=new Handler();using var client=new CommunityClient(new("https://test.supabase.co","sb_publishable_test"),root,handler);
            var cover=await client.DownloadCover("../../invalid-name",root);var again=await client.DownloadCover("../../invalid-name",root);
            if(cover!=again||handler.Requests!=2||Path.GetDirectoryName(cover)!=root)throw new Exception("Cache/path boundary failed");
            var preview=await client.DownloadPreview("../../invalid-name",root);if(preview==cover||handler.Requests!=4)throw new Exception("Preview collided with cover");
            handler.External=true;try{await client.DownloadCover("external",root);throw new Exception("External storage accepted");}catch(InvalidDataException){}
            handler.External=false;handler.Empty=true;try{await client.DownloadCover("empty",root);throw new Exception("Empty image cached");}catch(InvalidDataException){}
            for(int i=0;i<205;i++)File.WriteAllBytes(Path.Combine(root,"old-"+i+".img"),[1]);
            handler.Empty=false;await client.DownloadCover("new",root);if(Directory.GetFiles(root,"*.img").Length>200)throw new Exception("Unbounded image cache");
        }finally{Directory.Delete(root,true);}
    }
    private sealed class Handler:HttpMessageHandler
    {
        public int Requests;public bool External,Empty;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requests++;
            HttpContent body=request.RequestUri!.AbsolutePath.StartsWith("/functions/")?JsonContent.Create(new{url=External?"https://untrusted.invalid/image":"https://test.supabase.co/storage/image"}):new ByteArrayContent(Empty?[]:[1,2,3]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=body});
        }
    }
}
