import { createClient } from 'npm:@supabase/supabase-js@2';
import { unzipSync } from 'npm:fflate@0.8.2';

const url=Deno.env.get('SUPABASE_URL')!;
const publicKey=Deno.env.get('SUPABASE_ANON_KEY')!;
const server=createClient(url,Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!,{auth:{persistSession:false}});
const cors={'Access-Control-Allow-Origin':Deno.env.get('MYVOICE_SITE_ORIGIN')||'https://mattheube.github.io','Access-Control-Allow-Headers':'authorization,apikey,content-type','Access-Control-Allow-Methods':'GET,POST,OPTIONS'};
const response=(value:unknown,status=200)=>new Response(JSON.stringify(value),{status,headers:{...cors,'Content-Type':'application/json','Cache-Control':'no-store'}});
const hex=(b:ArrayBuffer)=>Array.from(new Uint8Array(b),v=>v.toString(16).padStart(2,'0')).join('');
const safe=(name:string)=>!name.startsWith('/')&&!/[\\:\x00]/.test(name)&&name.split('/').every(p=>p&&p!=='.'&&p!=='..'&&!/[. ]$/.test(p)&&!/^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\.|$)/i.test(p))&&/\.(json|wav|mp3|flac|ogg|m4a|png|jpg|jpeg|webp)$/i.test(name);

Deno.serve(async req=>{
 if(req.method==='OPTIONS')return new Response(null,{headers:cors});
 const authorization=req.headers.get('Authorization')||'';
 const userClient=createClient(url,publicKey,{global:{headers:authorization?{Authorization:authorization}:{}},auth:{persistSession:false}});
 try{
  if(req.method==='GET'){
   const id=new URL(req.url).searchParams.get('id');if(!id||!/^[a-f0-9-]{36}$/i.test(id))return response({error:'Invalid content ID'},400);
   const {data,error}=await userClient.rpc('package_for_download',{target:id});if(error||!data)return response({error:'Content unavailable'},404);
   const download=await server.storage.from('community').createSignedUrl(data.object_path,60);
   if(download.error)throw Error('Storage unavailable');
   return response({url:download.data.signedUrl,sha256:data.sha256,size:data.size,version:data.version});
  }
  if(req.method!=='POST')return response({error:'Method not allowed'},405);
  const {data:{user},error:authError}=await userClient.auth.getUser();
  if(authError||!user)return response({error:'Sign in first'},401);
  const allowed=await userClient.rpc('allow_publication');if(allowed.error)return response({error:'Verify your email or wait before publishing again'},403);
  if(Number(req.headers.get('content-length')||0)>134300000)return response({error:'Package too large'},413);
  // Enforce actual body size even when Content-Length is missing.
  const reader=req.body?.getReader();if(!reader)return response({error:'Package missing'},400);
  const chunks:Uint8Array[]=[];let length=0;
  while(true){const {value,done}=await reader.read();if(done)break;length+=value.length;if(length>134217728){await reader.cancel();return response({error:'Package too large'},413);}chunks.push(value);}
  const bytes=new Uint8Array(length);let offset=0;for(const chunk of chunks){bytes.set(chunk,offset);offset+=chunk.length;}
  const info=JSON.parse(new URL(req.url).searchParams.get('metadata')||'{}');
  if(info.rights_confirmed!==true||!['public','friends','unlisted','private'].includes(info.visibility))return response({error:'Rights confirmation and visibility required'},400);
  let expanded=0,count=0;const names=new Set<string>();
  const files=unzipSync(bytes,{filter:file=>{
   count++;expanded+=file.originalSize;
   if(count>512||expanded>268435456||file.originalSize>67108864||!safe(file.name)||names.has(file.name.toLowerCase()))throw Error('Unsafe package');
   names.add(file.name.toLowerCase());return true;
  }});
  if(!files['manifest.json']||files['manifest.json'].length>1048576)throw Error('Invalid manifest');
  const raw=JSON.parse(new TextDecoder().decode(files['manifest.json']));
  const m=Object.fromEntries(Object.entries(raw).map(([key,value])=>[key.toLowerCase(),value])) as any;
  if(m.schemaversion!==1||!['soundboard','voice-preset'].includes(m.kind)||typeof m.title!=='string'||!m.title.trim()||m.title.length>100||!/^\d+\.\d+\.\d+$/.test(m.version)||typeof m.files!=='object')throw Error('Incompatible package');
  for(const [name,content] of Object.entries(files)){
   if(name==='manifest.json')continue;
   if(hex(await crypto.subtle.digest('SHA-256',content)).toLowerCase()!==String(m.files[name]).toLowerCase())throw Error('Hash mismatch');
  }
  for(const name of Object.keys(m.files))if(!safe(name)||!files[name])throw Error('Missing file');
  const normalize=(v:unknown)=>v&&typeof v==='object'&&!Array.isArray(v)?Object.fromEntries(Object.entries(v).map(([k,x])=>[k.toLowerCase(),x])):null;
  const reference=(v:unknown)=>typeof v==='string'&&safe(v)&&Object.hasOwn(m.files,v)&&!!files[v];
  if(m.cover!=null&&!reference(m.cover))throw Error('Invalid cover');
  if(!Array.isArray(m.sounds)||m.sounds.length>200)throw Error('Invalid sounds');
  for(const rawSound of m.sounds){const sound=normalize(rawSound);if(!sound||!reference(sound.file)||(sound.cover!=null&&!reference(sound.cover))||typeof sound.name!=='string'||sound.name.length>200||!Number.isFinite(sound.volume)||Number(sound.volume)<0||!Number.isFinite(sound.bassdb)||!Number.isFinite(sound.saturation))throw Error('Invalid sound');}
  if(m.kind==='voice-preset'){const preset=normalize(m.preset);if(!preset||!['pitch','bass','mid','treble','delay','distortion','mix'].every(k=>Number.isFinite(preset[k]))||Number(preset.pitch)<.5||Number(preset.pitch)>2||Number(preset.mix)<0||Number(preset.mix)>1)throw Error('Invalid preset');}
  const description=String(info.description||'').slice(0,2000),tags=Array.isArray(info.tags)?info.tags.slice(0,8).map((x:unknown)=>String(x).slice(0,30)):[];
  const id=crypto.randomUUID();const objectPath=`${user.id}/${id}/${m.version}.zip`;
  const sha256=hex(await crypto.subtle.digest('SHA-256',bytes));
  const upload=await server.storage.from('community').upload(objectPath,bytes,{contentType:'application/zip',upsert:false});if(upload.error)throw Error('Upload failed');
  // Roll back storage on a failed metadata transaction. Only server can publish.
  const insert=await server.from('content').insert({id,owner_id:user.id,kind:m.kind,title:m.title,description,tags,language:String(info.language||'und').slice(0,12),visibility:info.visibility,status:'draft'});
  if(insert.error){await server.storage.from('community').remove([objectPath]);throw Error('Metadata rejected');}
  const version=await server.from('content_versions').insert({content_id:id,version:m.version,object_path:objectPath,sha256,size:bytes.length});
  if(version.error){await server.from('content').delete().eq('id',id);await server.storage.from('community').remove([objectPath]);throw Error('Version rejected');}
  const publish=await server.from('content').update({status:'published'}).eq('id',id);if(publish.error)throw Error('Publication incomplete');
  return response({id,version:m.version,url:`https://mattheube.github.io/MyVoice/#content/${id}`},201);
 }catch{return response({error:'Package rejected or service unavailable'},400);}
});
