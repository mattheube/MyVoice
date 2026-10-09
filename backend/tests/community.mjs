import {PGlite} from '@electric-sql/pglite';
import fs from 'node:fs';
const db=new PGlite();
await db.exec(`create role anon; create role authenticated; create role service_role bypassrls; create schema auth;create schema storage;create table auth.users(id uuid primary key,raw_user_meta_data jsonb,email_confirmed_at timestamptz);create function auth.uid()returns uuid language sql stable as $$select nullif(current_setting('request.jwt.claim.sub',true),'')::uuid$$;grant usage on schema auth to anon,authenticated;grant execute on function auth.uid() to anon,authenticated;create table storage.buckets(id text primary key,name text,public boolean,file_size_limit bigint,allowed_mime_types text[]);`);
await db.exec(fs.readFileSync('backend/supabase/migrations/202610090001_community.sql','utf8'));
await db.exec(fs.readFileSync('backend/supabase/migrations/202610090002_social_overview.sql','utf8'));
const ids=['00000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000003'];
for(let i=0;i<3;i++)await db.query('insert into auth.users values($1,$2,now())',[ids[i],JSON.stringify({username:['ALICE','bobby','carol'][i],display_name:'Same display name'})]);
const eq=(ok,name)=>{if(!ok)throw Error(name);console.log('PASS '+name)};
async function denied(sql,args,name){try{await db.query(sql,args);throw Error('NOT DENIED '+name)}catch(e){if(e.message.startsWith('NOT DENIED'))throw e;console.log('PASS '+name)}}
await denied('insert into auth.users values(gen_random_uuid(),$1,now())',[JSON.stringify({username:'Alice',display_name:'Different'})],'case-insensitive duplicate username rejected');
await denied('insert into auth.users values(gen_random_uuid(),$1,now())',[JSON.stringify({username:'admin',display_name:'Admin'})],'reserved handle rejected');
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
await db.close();
