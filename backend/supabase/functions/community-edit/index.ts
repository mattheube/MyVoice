import {createClient} from 'npm:@supabase/supabase-js@2.99.1';
const url=Deno.env.get('SUPABASE_URL')!;
const server=createClient(url,Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!,{auth:{persistSession:false}});
const cors={'Access-Control-Allow-Origin':'https://mattheube.github.io','Access-Control-Allow-Headers':'authorization,apikey,content-type','Access-Control-Allow-Methods':'POST,OPTIONS'};
const reply=(value:unknown,status=200)=>new Response(JSON.stringify(value),{status,headers:{...cors,'Content-Type':'application/json','Cache-Control':'no-store'}});
import {decode,previewWave} from './attachments.ts';
Deno.serve(async req=>{
 if(req.method==='OPTIONS')return new Response(null,{headers:cors});
 if(req.method!=='POST')return reply({error:'Method not allowed'},405);
 const token=(req.headers.get('Authorization')||'').replace(/^Bearer /i,'');
 const {data:{user},error}=await server.auth.getUser(token);if(error||!user)return reply({error:'Sign in first'},401);
 const userClient=createClient(url,Deno.env.get('SUPABASE_ANON_KEY')!,{global:{headers:{Authorization:'Bearer '+token}},auth:{persistSession:false}});
 const uploaded:string[]=[];
 try{
  const reader=req.body?.getReader();if(!reader)throw Error('Missing body');const chunks:Uint8Array[]=[];let length=0;
  while(true){const {value,done}=await reader.read();if(done)break;length+=value.length;if(length>12000000){await reader.cancel();return reply({error:'Request too large'},413);}chunks.push(value);}
  const body=new Uint8Array(length);let offset=0;for(const chunk of chunks){body.set(chunk,offset);offset+=chunk.length;}
  const info=JSON.parse(new TextDecoder().decode(body));if(!/^[a-f0-9-]{36}$/i.test(info.id||''))throw Error('Invalid publication');
  const {data:item,error:lookup}=await server.from('content').select('owner_id,status').eq('id',info.id).single();
  if(lookup||item.owner_id!==user.id||item.status!=='published')return reply({error:'Publication unavailable'},404);
  let cover:string|null=null,preview:string|null=null;
  if(info.cover!=null){const bytes=decode(info.cover,2000000);if(bytes.length<4||bytes[0]!==255||bytes[1]!==216||bytes.at(-2)!==255||bytes.at(-1)!==217)throw Error('JPEG required');cover=`${user.id}/${info.id}/cover-${crypto.randomUUID().replaceAll('-','')}.jpg`;const {error}=await server.storage.from('community').upload(cover,bytes,{contentType:'image/jpeg',upsert:false});if(error)throw error;uploaded.push(cover);}
  if(info.preview!=null){const bytes=decode(info.preview,6000000);previewWave(bytes);preview=`${user.id}/${info.id}/preview-${crypto.randomUUID().replaceAll('-','')}.wav`;const {error}=await server.storage.from('community').upload(preview,bytes,{contentType:'audio/wav',upsert:false});if(error)throw error;uploaded.push(preview);}
  const {data,error:edited}=await userClient.rpc('edit_publication',{target:info.id,new_title:info.title,new_description:info.description,new_tags:info.tags,new_category:info.category,new_language:info.language,new_visibility:info.visibility,new_cover:cover,new_preview:preview});
  if(edited)throw edited;
  return reply(data);
 }catch{
  if(uploaded.length)await server.storage.from('community').remove(uploaded);
  return reply({error:'Changes rejected. Check ownership, fields and attachment formats.'},400);
 }
});
