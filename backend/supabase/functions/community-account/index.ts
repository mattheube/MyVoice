import {createClient} from 'npm:@supabase/supabase-js@2.99.1';
const url=Deno.env.get('SUPABASE_URL')!;
const server=createClient(url,Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!,{auth:{persistSession:false}});
Deno.serve(async req=>{
 const headers={'Content-Type':'application/json','Cache-Control':'no-store'};
 const reply=(status:number,message:string)=>new Response(JSON.stringify({message}),{status,headers});
 if(req.method!=='DELETE')return reply(405,'Method not allowed');
 const token=(req.headers.get('Authorization')||'').replace(/^Bearer /i,'');if(!token)return reply(401,'Sign in first');
 const {data:{user},error}=await server.auth.getUser(token);if(error||!user)return reply(401,'Sign in first');
 // The target is always the verified caller, never a request parameter.
 try{
  const {data:contents,error:ce}=await server.from('content').select('id').eq('owner_id',user.id);if(ce)throw ce;
  for(const content of contents||[]){const {data:versions,error:ve}=await server.from('content_versions').select('object_path,cover_path').eq('content_id',content.id);if(ve)throw ve;const paths=(versions||[]).flatMap(v=>[v.object_path,v.cover_path].filter(Boolean));if(paths.some(p=>!p.startsWith(user.id+'/')))throw Error('Invalid ownership');if(paths.length){const {error}=await server.storage.from('community').remove(paths);if(error)throw error;}}
  const signedOut=await server.auth.admin.signOut(token,'global');if(signedOut.error)throw signedOut.error;
  const removed=await server.auth.admin.deleteUser(user.id);if(removed.error)throw removed.error;
  return reply(200,'Account removed');
 }catch{return reply(400,'Account removal incomplete; please retry');}
});
