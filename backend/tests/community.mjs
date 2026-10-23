import {PGlite} from '@electric-sql/pglite';
import fs from 'node:fs';
const db=new PGlite();
await db.exec(`create role anon; create role authenticated; create role service_role bypassrls; create schema auth;create schema storage;create table auth.users(id uuid primary key,raw_user_meta_data jsonb,email_confirmed_at timestamptz);create function auth.uid()returns uuid language sql stable as $$select nullif(current_setting('request.jwt.claim.sub',true),'')::uuid$$;grant usage on schema auth to anon,authenticated;grant execute on function auth.uid() to anon,authenticated;create table storage.buckets(id text primary key,name text,public boolean,file_size_limit bigint,allowed_mime_types text[]);`);
await db.exec(fs.readFileSync('backend/supabase/migrations/202610090001_community.sql','utf8'));
await db.exec(fs.readFileSync('backend/supabase/migrations/202610090002_social_overview.sql','utf8'));
await db.exec(fs.readFileSync('backend/supabase/migrations/202610130001_publication_ownership.sql','utf8'));
await db.exec(fs.readFileSync('backend/supabase/migrations/20261018005355_focused_community_profile.sql','utf8'));
await db.exec(fs.readFileSync('backend/supabase/migrations/20261019173116_profile_activity_lists.sql','utf8'));
await db.exec(fs.readFileSync('backend/supabase/migrations/20261019214850_profile_activity_privacy.sql','utf8'));
await db.exec(`alter table auth.users add column raw_app_meta_data jsonb;`);
await db.exec(fs.readFileSync('backend/supabase/migrations/20261023194232_discord_profiles.sql','utf8'));
const ids=['00000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000003'];
for(let i=0;i<3;i++)await db.query('insert into auth.users(id,raw_user_meta_data,email_confirmed_at) values($1,$2,now())',[ids[i],JSON.stringify({username:['ALICE','bobby','carol'][i],display_name:'Same display name'})]);
const eq=(ok,name)=>{if(!ok)throw Error(name);console.log('PASS '+name)};
async function denied(sql,args,name){try{await db.query(sql,args);throw Error('NOT DENIED '+name)}catch(e){if(e.message.startsWith('NOT DENIED'))throw e;console.log('PASS '+name)}}
await denied('insert into auth.users(id,raw_user_meta_data,email_confirmed_at) values(gen_random_uuid(),$1,now())',[JSON.stringify({username:'Alice',display_name:'Different'})],'case-insensitive duplicate username rejected');
await denied('insert into auth.users(id,raw_user_meta_data,email_confirmed_at) values(gen_random_uuid(),$1,now())',[JSON.stringify({username:'admin',display_name:'Admin'})],'reserved handle rejected');
eq((await db.query('select count(*)::int n from public.profiles')).rows[0].n===3,'duplicate display names allowed');
async function as(i){await db.exec('reset role');await db.query("select set_config('request.jwt.claim.sub',$1,false)",[i==null?'':ids[i]]);await db.exec('set role '+(i==null?'anon':'authenticated'));}
await as(0);await denied('update public.profiles set verified=true where id=$1',[ids[0]],'client cannot award verified badge');
await db.query('update public.profiles set private=true,bio=$1 where id=$2',['secret',ids[0]]);
await as(1);eq((await db.query('select * from public.profiles where id=$1',[ids[0]])).rows.length===0,'private profile hidden');
eq((await db.query('select public.follow_creator($1)',[ids[0]])).rows[0].follow_creator==='Requested','private follow needs approval');
await as(0);await db.query('select public.answer_follow($1,true)',[ids[1]]);eq((await db.query('select public.follow_creator($1)',[ids[1]])).rows[0].follow_creator==='Friends','mutual accepted follows become friends');
await db.exec('reset role');const content=[];
for(const visibility of ['public','friends','private','unlisted']){const r=await db.query("insert into public.content(owner_id,kind,title,visibility,status) values($1,'soundboard',$2,$2,'published') returning id",[ids[0],visibility]);content.push(r.rows[0].id);await db.query("insert into public.content_versions(content_id,version,object_path,sha256,size) values($1,'1.0.0',$2,$3,20)",[r.rows[0].id,r.rows[0].id+'.zip','0'.repeat(64)]);}
await as(null);eq((await db.query('select * from public.content')).rows.length===1,'anonymous feed only public');
eq((await db.query('select public.package_for_download($1)',[content[1]])).rows[0].package_for_download===null,'friends package forbidden anonymously');
eq((await db.query('select public.content_by_id($1)',[content[3]])).rows[0].content_by_id!==null,'unlisted accessible by id only');
await as(1);eq((await db.query('select public.package_for_download($1)',[content[1]])).rows[0].package_for_download!==null,'friend can download friends-only package');
eq((await db.query('select public.package_for_download($1)',[content[2]])).rows[0].package_for_download===null,'private package forbidden to friend');
await db.query("select public.react_to_content($1,'save')",[content[1]]);await db.query("select public.react_to_content($1,'like')",[content[1]]);
await as(0);await db.query('select public.block_creator($1)',[ids[1]]);
await as(1);eq((await db.query('select public.package_for_download($1)',[content[1]])).rows[0].package_for_download===null,'block revokes friends package access');await denied('select public.follow_creator($1)',[ids[0]],'blocked user cannot follow');await denied("select public.moderate_content($1,'remove')",[content[0]],'user cannot moderate');
await as(2);await db.exec('reset role');await db.query('update auth.users set email_confirmed_at=null where id=$1',[ids[2]]);await as(2);await denied('select public.allow_publication()',[],'unverified email cannot publish');
await as(0);for(let i=0;i<3;i++)await db.query('select public.allow_publication()');await denied('select public.allow_publication()',[],'publication rate limit');
await denied('select public.moderation_overview()',[],'reports hidden from ordinary users');
await denied("select public.admin_creator($1,'verify')",[ids[1]],'users cannot verify creators');


await db.exec('reset role');
await db.query('select public.claim_publication_assets($1,$2)',[ids[0],['a'.repeat(64)]]);
await db.query('select public.claim_publication_assets($1,$2)',[ids[0],['a'.repeat(64)]]);
eq(true,'same creator may reuse their own asset');
await denied('select public.claim_publication_assets($1,$2)',[ids[1],['a'.repeat(64)]],'another creator cannot claim the same audio hash');
await as(1);await denied('select public.claim_publication_assets($1,$2)',[ids[1],['b'.repeat(64)]],'client cannot directly assign asset ownership');

await as(0);
const own=(await db.query("select public.community_browse(own_only=>true) r")).rows[0].r;
eq(own.length===4,'owner management includes private/unlisted');
const feed=(await db.query("select public.community_browse() r")).rows[0].r;
eq(feed.every(c=>['public','friends'].includes(c.visibility)),'discovery excludes even own private/unlisted');
await denied("update public.profiles set username='admin' where id=$1",[ids[0]],'cannot rename profile to reserved handle');
await db.exec('reset role');
await db.query("insert into public.content(owner_id,kind,title,description,tags,visibility,status) values($1,'ai-voice','Michou','Test',array['gaming'],'public','published'),($1,'voice-preset','Michou remix','Description',array['voice'],'public','published')",[ids[2]]);
await as(null);
let results=(await db.query("select public.community_browse('Michou') r")).rows[0].r;
eq(results[0].title==='Michou','exact community title ranks first');
eq((await db.query("select public.community_browse('Micho') r")).rows[0].r.some(c=>c.title==='Michou'),'missing letter tolerated');
eq((await db.query("select public.community_browse('GÁMING') r")).rows[0].r.some(c=>c.title==='Michou'),'accent-insensitive tag search');
eq((await db.query("select public.community_browse('carol') r")).rows[0].r.length===2,'creator search');
eq((await db.query("select public.profile_totals($1) r",[ids[0]])).rows[0].r===null,'private profile statistics withheld');
await as(0);eq((await db.query("select public.profile_totals($1) r",[ids[0]])).rows[0].r.likes>=1,'owner receives actual aggregate likes');
await as(null);await denied("select public.profile_activity('followers')",[],'anonymous cannot enumerate profile activity');
await as(0);await denied("select public.profile_activity('invalid')",[],'unknown activity list rejected');
const received=(await db.query("select public.profile_activity('likes') r")).rows[0].r;
eq(received.every(x=>x.id&&x.username&&x.detail),'received likes contain only display identities and publication titles');
await db.exec('reset role');
await db.query("insert into auth.users select gen_random_uuid(),jsonb_build_object('username','follower'||n,'display_name','Follower '||n),now() from generate_series(1,65) n");
await db.query("insert into public.follows(follower,following,accepted) select id,$1,true from public.profiles where username like 'follower%'",[ids[0]]);
await db.exec("update public.profiles set private=true where username='follower1'");
await as(0);eq(!(await db.query("select public.profile_activity('followers') r")).rows[0].r.some(x=>x.username==='follower1'),'private follower identity is not exposed without an accepted follow');
await as(0);eq((await db.query("select jsonb_array_length(public.profile_activity('followers')) n")).rows[0].n===50,'large follower list capped at server to 50');
await as(1);eq((await db.query("select public.profile_activity('followers') r")).rows[0].r.every(x=>!x.username.startsWith('follower')),'activity always scoped to authenticated owner');


await db.exec('reset role');
const discordId='00000000-0000-0000-0000-000000000099';
await db.query("insert into auth.users(id,raw_user_meta_data,raw_app_meta_data,email_confirmed_at) values($1,$2,$3,now())",[discordId,JSON.stringify({username:'admin',full_name:'Discord Creator',verified:true}),JSON.stringify({provider:'discord'})]);
const discordProfile=(await db.query('select username,display_name,verified from public.profiles where id=$1',[discordId])).rows[0];
eq(/^user_[a-f0-9]{19}$/.test(discordProfile.username)&&discordProfile.display_name==='Discord Creator'&&!discordProfile.verified,'Discord receives a safe handle and no trusted badges from user metadata');
await denied("insert into auth.users(id,raw_user_meta_data,raw_app_meta_data) values(gen_random_uuid(),$1,'{}')",[JSON.stringify({provider:'discord',username:'admin'})],'user metadata cannot impersonate a trusted OAuth provider');



await db.exec('reset role');
await db.exec('alter default privileges grant execute on functions to anon,authenticated;');
await db.exec(fs.readFileSync('backend/supabase/migrations/20261023194508_publication_edits.sql','utf8'));
await db.exec(fs.readFileSync('backend/supabase/migrations/20261023195443_publication_edit_grants.sql','utf8'));
eq(!(await db.query("select has_function_privilege('anon','public.edit_publication(uuid,text,text,text[],text,text,text,text,text)','EXECUTE') allowed")).rows[0].allowed,'Supabase default function grants cannot expose publication editing anonymously');
await as(0);
const edited=(await db.query("select public.edit_publication($1,'Edited title','Description',array['test'],'Other','fr','private') r",[content[0]])).rows[0].r;
eq(edited.title==='Edited title'&&edited.audio_locked,'public content remains permanently locked after becoming private');
await as(1);await denied("select public.edit_publication($1,'Stolen','',array[]::text[],'Other','fr','public')",[content[0]],'another account cannot edit a publication');
await as(null);await denied("select public.edit_publication($1,'Anon','',array[]::text[],'Other','fr','public')",[content[0]],'anonymous cannot edit');
await as(0);await denied("select public.edit_publication($1,'X','',array[]::text[],'Other','fr','private',$2)",[content[0],ids[1]+'/'+content[0]+'/cover-'+('a'.repeat(32))+'.jpg'],'cover cannot reference another owner');
await denied("select public.replace_private_audio($1,$2,$3,$4,$5,100,'1.0.1')",[content[2],ids[0],'a'.repeat(64),'x','b'.repeat(64)],'client cannot bypass upload verification');
await db.exec('reset role');
for(const id of [content[0],content[1],content[3]]){
 await denied("update public.content_versions set sha256=$2 where content_id=$1",[id,'b'.repeat(64)],'shared audio version cannot be replaced');
}
await db.query('update public.content set audio_locked=false where id=$1',[content[0]]);
eq((await db.query('select audio_locked from public.content where id=$1',[content[0]])).rows[0].audio_locked,'lock cannot be cleared');
await db.query('update public.content_versions set sha256=$2 where content_id=$1',[content[2],'b'.repeat(64)]);
eq((await db.query('select sha256 from public.content_versions where content_id=$1',[content[2]])).rows[0].sha256==='b'.repeat(64),'never-shared private audio may change');


await db.query("select public.replace_private_audio($1,$2,$3,$4,$5,100,'1.0.1')",[content[2],ids[0],'b'.repeat(64),ids[0]+'/'+content[2]+'/replacement.zip','c'.repeat(64)]);
eq((await db.query('select sha256 from public.content_versions where content_id=$1',[content[2]])).rows[0].sha256==='c'.repeat(64),'verified service transaction replaces private audio');
await denied("select public.replace_private_audio($1,$2,$3,$4,$5,100,'1.0.2')",[content[2],ids[0],'b'.repeat(64),ids[0]+'/'+content[2]+'/race.zip','d'.repeat(64)],'stale replacement cannot overwrite concurrent audio');
await as(0);
await db.query("select public.edit_publication($1,'Ready','',array[]::text[],'Other','fr','friends',$2,$3)",[content[2],ids[0]+'/'+content[2]+'/cover-'+('a'.repeat(32))+'.jpg',ids[0]+'/'+content[2]+'/preview-'+('b'.repeat(32))+'.wav']);
await db.exec('reset role');
await denied("select public.replace_private_audio($1,$2,$3,$4,$5,100,'1.0.2')",[content[2],ids[0],'c'.repeat(64),ids[0]+'/'+content[2]+'/later.zip','d'.repeat(64)],'sharing atomically prevents later private audio replacement');
await db.query('delete from auth.users where id=$1',[ids[0]]);
eq((await db.query('select count(*)::int n from public.content where owner_id=$1',[ids[0]])).rows[0].n===0,'account deletion can cascade through locked audio');
await db.exec(fs.readFileSync('backend/supabase/migrations/20261023201020_profile_banner_username.sql','utf8'));
await as(1);await db.query('update public.profiles set banner=$1,private=true where id=$2',['data:image/jpeg;base64,AAAA',ids[1]]);
eq((await db.query("select public.creator_by_username('bobby') r")).rows[0].r.banner==='data:image/jpeg;base64,AAAA','owner can read own private banner');
await denied('update public.profiles set banner=$1 where id=$2',['data:image/svg+xml;base64,AAAA',ids[1]],'banner rejects active image types');
await as(null);eq((await db.query("select public.creator_by_username('bobby') r")).rows[0].r.banner===null,'private banner hidden anonymously');
for(const [handle,expected] of [['bobby',false],['admin',false],['unused_name_2026',true],['bad name',false]])eq((await db.query('select public.username_available($1) available',[handle])).rows[0].available===expected,'username availability: '+handle);
await as(2);const changed=await db.query('update public.profiles set banner=$1 where id=$2 returning id',['data:image/jpeg;base64,BBBB',ids[1]]);eq(changed.rows.length===0,'another account cannot edit a banner');
await db.close();
