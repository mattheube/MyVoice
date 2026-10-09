-- MyVoice community. Run once in a new Supabase project. Audio remains local.
create schema if not exists myvoice_private;
revoke all on schema myvoice_private from public;
grant usage on schema myvoice_private to anon, authenticated;

create table public.profiles (
 id uuid primary key references auth.users(id) on delete cascade,
 username text not null check(username ~ '^[a-z0-9_]{3,24}$'),
 display_name text not null check(length(display_name) between 1 and 60),
 bio text not null default '' check(length(bio)<=300),
 avatar text, private boolean not null default false,
 verified boolean not null default false,
 created_at timestamptz not null default now(),
 unique(username)
);
create table myvoice_private.roles(user_id uuid primary key references public.profiles(id) on delete cascade,role text not null check(role in ('user','moderator','admin','official')),suspended boolean not null default false);
create table public.follows(follower uuid references public.profiles(id) on delete cascade,following uuid references public.profiles(id) on delete cascade,accepted boolean not null default false,created_at timestamptz not null default now(),primary key(follower,following),check(follower<>following));
create table public.blocks(blocker uuid references public.profiles(id) on delete cascade,blocked uuid references public.profiles(id) on delete cascade,primary key(blocker,blocked),check(blocker<>blocked));
create table public.content(id uuid primary key default gen_random_uuid(),owner_id uuid not null references public.profiles(id) on delete cascade,kind text not null check(kind in ('soundboard','voice-preset','ai-voice')),title text not null check(length(title) between 1 and 100),description text not null default '' check(length(description)<=2000),tags text[] not null default '{}',language text not null default 'und',visibility text not null check(visibility in ('public','friends','unlisted','private')),status text not null default 'draft' check(status in ('draft','published','removed')),featured boolean not null default false,created_at timestamptz not null default now());
create table public.content_versions(id uuid primary key default gen_random_uuid(),content_id uuid not null references public.content(id) on delete cascade,version text not null check(version ~ '^[0-9]+\.[0-9]+\.[0-9]+$'),object_path text not null unique,sha256 text not null check(sha256 ~ '^[a-fA-F0-9]{64}$'),size bigint not null check(size between 1 and 134217728),created_at timestamptz not null default now(),unique(content_id,version));
create table public.likes(user_id uuid references public.profiles(id) on delete cascade,content_id uuid references public.content(id) on delete cascade,primary key(user_id,content_id));
create table public.saves(user_id uuid references public.profiles(id) on delete cascade,content_id uuid references public.content(id) on delete cascade,primary key(user_id,content_id));
create table public.reports(id uuid primary key default gen_random_uuid(),reporter uuid not null references public.profiles(id) on delete cascade,content_id uuid references public.content(id) on delete cascade,reason text not null check(reason in ('copyright','impersonation','harmful','spam','other')),details text not null default '' check(length(details)<=1500),created_at timestamptz not null default now());
create table myvoice_private.rate_events(user_id uuid not null,action text not null,created_at timestamptz not null default now());
create index on myvoice_private.rate_events(user_id,action,created_at);
create index on public.content(visibility,status,created_at desc);
create index on public.follows(following,accepted);

create function myvoice_private.blocked(a uuid,b uuid) returns boolean language sql stable security definer set search_path='' as $$select exists(select 1 from public.blocks where (blocker=a and blocked=b) or(blocker=b and blocked=a))$$;
create function myvoice_private.friends(a uuid,b uuid) returns boolean language sql stable security definer set search_path='' as $$select a is not null and b is not null and not myvoice_private.blocked(a,b) and exists(select 1 from public.follows where follower=a and following=b and accepted) and exists(select 1 from public.follows where follower=b and following=a and accepted)$$;
create function myvoice_private.can_read_content(target uuid,allow_unlisted boolean default false) returns boolean language sql stable security definer set search_path='' as $$
 select exists(select 1 from public.content c where c.id=target and (c.owner_id=auth.uid() or(c.status='published' and not myvoice_private.blocked(auth.uid(),c.owner_id) and(c.visibility='public' or(c.visibility='unlisted' and allow_unlisted) or(c.visibility='friends' and myvoice_private.friends(auth.uid(),c.owner_id))))))
$$;
create function myvoice_private.rate_limit(action_name text,maximum integer) returns void language plpgsql security definer set search_path='' as $$
begin
 if auth.uid() is null then raise exception 'Authentication required';end if;
 perform pg_advisory_xact_lock(hashtext(auth.uid()::text||action_name));
 if exists(select 1 from myvoice_private.roles where user_id=auth.uid() and suspended) then raise exception 'Account suspended';end if;
 if(select count(*) from myvoice_private.rate_events where user_id=auth.uid() and action=action_name and created_at>now()-interval '1 minute')>=maximum then raise exception 'Please slow down';end if;
 insert into myvoice_private.rate_events(user_id,action) values(auth.uid(),action_name);
 delete from myvoice_private.rate_events where user_id=auth.uid() and created_at<now()-interval '1 day';
end$$;
create function myvoice_private.new_profile() returns trigger language plpgsql security definer set search_path='' as $$
declare handle text:=lower(trim(new.raw_user_meta_data->>'username')); label text:=trim(new.raw_user_meta_data->>'display_name');
begin
 if handle is null or handle !~ '^[a-z0-9_]{3,24}$' or handle in('myvoice','admin','administrator','support','moderator','official','system') then raise exception 'Username unavailable';end if;
 insert into public.profiles(id,username,display_name) values(new.id,handle,coalesce(nullif(label,''),handle));
 return new;
end$$;
create trigger myvoice_profile after insert on auth.users for each row execute function myvoice_private.new_profile();

alter table public.profiles enable row level security;
alter table public.follows enable row level security;
alter table public.blocks enable row level security;
alter table public.content enable row level security;
alter table public.content_versions enable row level security;
alter table public.likes enable row level security;
alter table public.saves enable row level security;
alter table public.reports enable row level security;
revoke all on public.profiles,public.follows,public.blocks,public.content,public.content_versions,public.likes,public.saves,public.reports from anon,authenticated;
grant select on public.profiles,public.content,public.content_versions to anon,authenticated;
grant select on public.follows,public.blocks,public.likes,public.saves,public.reports to authenticated;
grant update(display_name,bio,private,avatar) on public.profiles to authenticated;
create policy profiles_read on public.profiles for select using(id=auth.uid() or(not myvoice_private.blocked(auth.uid(),id) and(not private or exists(select 1 from public.follows where follower=auth.uid() and following=id and accepted))));
create policy profiles_edit on public.profiles for update to authenticated using(id=auth.uid()) with check(id=auth.uid());
create policy follows_read on public.follows for select to authenticated using(follower=auth.uid() or following=auth.uid());
create policy blocks_read on public.blocks for select to authenticated using(blocker=auth.uid());
create policy content_read on public.content for select using(myvoice_private.can_read_content(id,false));
create policy versions_read on public.content_versions for select using(myvoice_private.can_read_content(content_id,false));
create policy likes_read on public.likes for select to authenticated using(user_id=auth.uid());
create policy saves_read on public.saves for select to authenticated using(user_id=auth.uid());
create policy reports_read on public.reports for select to authenticated using(reporter=auth.uid());

create function public.follow_creator(target uuid,enabled boolean default true) returns text language plpgsql security definer set search_path='' as $$
declare hidden boolean;
begin
 perform myvoice_private.rate_limit('follow',20);
 if target=auth.uid() or myvoice_private.blocked(auth.uid(),target) then raise exception 'Follow unavailable';end if;
 if not enabled then delete from public.follows where follower=auth.uid() and following=target;return 'Follow';end if;
 select private into hidden from public.profiles where id=target;if not found then raise exception 'Creator missing';end if;
 insert into public.follows(follower,following,accepted)values(auth.uid(),target,not hidden) on conflict do nothing;
 if myvoice_private.friends(auth.uid(),target) then return 'Friends';end if;
 if hidden and not exists(select 1 from public.follows where follower=auth.uid() and following=target and accepted) then return 'Requested';end if;
 return 'Following';
end$$;
create function public.answer_follow(requester uuid,accept boolean) returns void language plpgsql security definer set search_path='' as $$
begin perform myvoice_private.rate_limit('follow',20);if myvoice_private.blocked(auth.uid(),requester)then raise exception 'Blocked';end if;
 if accept then update public.follows set accepted=true where follower=requester and following=auth.uid();else delete from public.follows where follower=requester and following=auth.uid();end if;end$$;
create function public.block_creator(target uuid,enabled boolean default true) returns void language plpgsql security definer set search_path='' as $$
begin perform myvoice_private.rate_limit('block',20);if target=auth.uid() then raise exception 'Invalid target';end if;
 if enabled then insert into public.blocks values(auth.uid(),target) on conflict do nothing;delete from public.follows where(follower=auth.uid() and following=target)or(follower=target and following=auth.uid());else delete from public.blocks where blocker=auth.uid() and blocked=target;end if;end$$;
create function public.react_to_content(target uuid,action text,enabled boolean default true) returns void language plpgsql security definer set search_path='' as $$
begin perform myvoice_private.rate_limit('reaction',30);if not myvoice_private.can_read_content(target,true)then raise exception 'Content unavailable';end if;
 if action='like' then if enabled then insert into public.likes values(auth.uid(),target) on conflict do nothing;else delete from public.likes where user_id=auth.uid() and content_id=target;end if;
 elsif action='save' then if enabled then insert into public.saves values(auth.uid(),target) on conflict do nothing;else delete from public.saves where user_id=auth.uid() and content_id=target;end if;else raise exception 'Invalid action';end if;end$$;
create function public.report_content(target uuid,reason text,details text default '') returns void language plpgsql security definer set search_path='' as $$
begin perform myvoice_private.rate_limit('report',5);if not myvoice_private.can_read_content(target,true) then raise exception 'Content unavailable';end if;insert into public.reports(reporter,content_id,reason,details) values(auth.uid(),target,reason,details);end$$;
create function public.creator_by_username(handle text) returns jsonb language plpgsql stable security definer set search_path='' as $$
declare p public.profiles; following boolean; reverse_follow boolean;
begin select * into p from public.profiles where username=lower(trim(handle));if not found or myvoice_private.blocked(auth.uid(),p.id)then return null;end if;
 following:=exists(select 1 from public.follows where follower=auth.uid() and following=p.id and accepted);
 reverse_follow:=exists(select 1 from public.follows where follower=p.id and following=auth.uid() and accepted);
 return jsonb_build_object('id',p.id,'username',p.username,'display_name',p.display_name,'private',p.private,'verified',p.verified,'bio',case when not p.private or p.id=auth.uid() or following then p.bio else '' end,'relationship',case when following and reverse_follow then 'Friends' when following then 'Following' when exists(select 1 from public.follows where follower=auth.uid() and following=p.id)then 'Requested' when p.private then 'Request Follow' when reverse_follow then 'Follow Back' else 'Follow' end);
end$$;
create function public.content_by_id(target uuid) returns jsonb language sql stable security definer set search_path='' as $$select to_jsonb(c) from public.content c where c.id=target and myvoice_private.can_read_content(target,true)$$;
create function public.package_for_download(target uuid) returns jsonb language sql stable security definer set search_path='' as $$select to_jsonb(v) from public.content_versions v where v.content_id=target and myvoice_private.can_read_content(target,true) order by created_at desc limit 1$$;
create function public.allow_publication() returns void language plpgsql security definer set search_path='' as $$
begin perform myvoice_private.rate_limit('publish',3);if not exists(select 1 from auth.users where id=auth.uid() and email_confirmed_at is not null)then raise exception 'Verify your email before publishing';end if;end$$;
create function public.moderate_content(target uuid,action text) returns void language plpgsql security definer set search_path='' as $$
begin
 if not exists(select 1 from myvoice_private.roles where user_id=auth.uid() and role in('admin','moderator') and not suspended)then raise exception 'Forbidden';end if;
 if action='remove' then update public.content set status='removed' where id=target;
 elsif action='feature' then update public.content set featured=true where id=target and status='published';else raise exception 'Invalid action';end if;
end$$;

-- Storage is private. Downloads are authorized on each Edge Function request.
insert into storage.buckets(id,name,public,file_size_limit,allowed_mime_types) values('community','community',false,134217728,array['application/zip']) on conflict(id) do nothing;
-- No client insert/read policy on this bucket: service-role only, behind the Edge Function.
revoke all on all functions in schema myvoice_private from public,anon,authenticated;
grant execute on function myvoice_private.blocked(uuid,uuid),myvoice_private.friends(uuid,uuid),myvoice_private.can_read_content(uuid,boolean) to anon,authenticated;
revoke all on function public.follow_creator(uuid,boolean),public.answer_follow(uuid,boolean),public.block_creator(uuid,boolean),public.react_to_content(uuid,text,boolean),public.report_content(uuid,text,text),public.allow_publication(),public.moderate_content(uuid,text),public.creator_by_username(text),public.content_by_id(uuid),public.package_for_download(uuid) from public,anon,authenticated;
grant execute on function public.follow_creator(uuid,boolean),public.answer_follow(uuid,boolean),public.block_creator(uuid,boolean),public.react_to_content(uuid,text,boolean),public.report_content(uuid,text,text),public.allow_publication(),public.moderate_content(uuid,text) to authenticated;
grant execute on function public.creator_by_username(text),public.content_by_id(uuid),public.package_for_download(uuid) to anon,authenticated;
grant all on all tables in schema public to service_role;
grant usage on schema myvoice_private to service_role;
