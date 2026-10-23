import {createClient} from 'npm:@supabase/supabase-js@2.99.1';
const url=Deno.env.get('SUPABASE_URL')!;
const server=createClient(url,Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!,{auth:{persistSession:false}});
async function removeOwnedObjects(prefix:string){
 // Read from offset zero after each deletion, so pagination cannot skip files.
 // Include obsolete covers/previews from earlier edits, not just current database pointers.
 while(true){
  const {data,error}=await server.storage.from('community').list(prefix,{limit:100,offset:0,sortBy:{column:'name',order:'asc'}});if(error)throw error;if(!data?.length)return;
  const files:string[]=[];
  for(const entry of data){if(!entry.name||entry.name==='.'||entry.name==='..'||/[\\/]/.test(entry.name))throw Error('Invalid object name');const path=prefix+'/'+entry.name;if(entry.id==null)await removeOwnedObjects(path);else files.push(path);}
  if(files.length){const {error}=await server.storage.from('community').remove(files);if(error)throw error;}
 }
}
Deno.serve(async req=>{
 const headers={'Content-Type':'application/json','Cache-Control':'no-store'};
 const reply=(status:number,message:string)=>new Response(JSON.stringify({message}),{status,headers});
 if(req.method!=='DELETE')return reply(405,'Method not allowed');
 const token=(req.headers.get('Authorization')||'').replace(/^Bearer /i,'');if(!token)return reply(401,'Sign in first');
 const {data:{user},error}=await server.auth.getUser(token);if(error||!user)return reply(401,'Sign in first');
 // The target is always the verified caller, never a request parameter.
 try{
  await removeOwnedObjects(user.id);
  const signedOut=await server.auth.admin.signOut(token,'global');if(signedOut.error)throw signedOut.error;
  const removed=await server.auth.admin.deleteUser(user.id);if(removed.error)throw removed.error;
  return reply(200,'Account removed');
 }catch{return reply(400,'Account removal incomplete; please retry');}
});
