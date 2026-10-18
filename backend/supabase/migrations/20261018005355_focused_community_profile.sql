-- Focused discovery/profile additions. Existing accounts, rows and visibility rules are preserved.
create function myvoice_private.search_normalize(value text) returns text language sql immutable set search_path='' as $$select regexp_replace(translate(lower(coalesce(value,'')),'àâäáãåéèêëíìîïóòôöõúùûüçÿ','aaaaaaeeeeiiiiooooouuuucy'),'[^a-z0-9 ]','','g')$$;
create function myvoice_private.search_distance(a text,b text) returns integer language plpgsql immutable set search_path='' as $$
declare prev integer[]; curr integer[]; i integer;j integer;
begin a:=left(a,100);b:=left(b,100);if a='' then return length(b);end if;if b='' then return length(a);end if;
 select array_agg(x) into prev from generate_series(0,length(b)) x;
 for i in 1..length(a) loop curr:=array[i];for j in 1..length(b) loop curr:=array_append(curr,least(curr[j]+1,prev[j+1]+1,prev[j]+case when substr(a,i,1)=substr(b,j,1) then 0 else 1 end));end loop;prev:=curr;end loop;return prev[length(b)+1];end$$;
create function myvoice_private.browse_creations(query_text text,kind_filter text,sort_order text,owner_filter uuid,own_only boolean) returns jsonb language sql stable security definer set search_path='' as $$
with candidates as (
 select c.*,p.username,p.verified,exists(select 1 from myvoice_private.roles rr where rr.user_id=p.id and rr.role='official' and not rr.suspended) official,
 (select count(*) from public.likes l where l.content_id=c.id) like_count,
 exists(select 1 from public.likes l where l.content_id=c.id and l.user_id=auth.uid()) liked,
 myvoice_private.search_normalize(query_text) q,myvoice_private.search_normalize(c.title) title_key,
 myvoice_private.search_normalize(concat_ws(' ',c.description,array_to_string(c.tags,' '),p.username,p.display_name)) metadata
 from public.content c join public.profiles p on p.id=c.owner_id
 where c.status='published' and myvoice_private.can_read_content(c.id,false)
 and (case when own_only then auth.uid() is not null and c.owner_id=auth.uid() else c.visibility in('public','friends') end)
 and (owner_filter is null or c.owner_id=owner_filter) and(kind_filter='all' or c.kind=kind_filter)
), ranked as (
 select *,case when q='' then 5 when title_key=q then 0 when title_key like q||'%' then 1 when title_key like '%'||q||'%' then 2 when metadata like '%'||q||'%' then 3 when length(q)>=3 and exists(select 1 from regexp_split_to_table(title_key||' '||metadata,' +') w where abs(length(w)-length(q))<=1 and myvoice_private.search_distance(w,q)<=1) then 4 else 9 end score from candidates
) select coalesce(jsonb_agg(to_jsonb(result)),'[]'::jsonb) from (
 select id,title,description,kind,owner_id,username,verified,official,tags,visibility,like_count,liked,created_at from ranked where score<9 order by score,case when sort_order='Popular' then like_count else 0 end desc,created_at desc limit 100
) result$$;
revoke all on function myvoice_private.browse_creations(text,text,text,uuid,boolean),myvoice_private.search_normalize(text),myvoice_private.search_distance(text,text) from public;
grant execute on function myvoice_private.browse_creations(text,text,text,uuid,boolean) to anon,authenticated;
create function public.community_browse(query_text text default '',kind_filter text default 'all',sort_order text default 'Recent',owner_filter uuid default null,own_only boolean default false) returns jsonb language sql stable security invoker set search_path='' as $$select myvoice_private.browse_creations(left(query_text,100),kind_filter,sort_order,owner_filter,own_only)$$;
revoke all on function public.community_browse(text,text,text,uuid,boolean) from public;
grant execute on function public.community_browse(text,text,text,uuid,boolean) to anon,authenticated;
create function myvoice_private.profile_totals(target uuid) returns jsonb language sql stable security definer set search_path='' as $$
select jsonb_build_object('followers',(select count(*) from public.follows where following=target and accepted),'following',(select count(*) from public.follows where follower=target and accepted),'likes',(select count(*) from public.likes l join public.content c on c.id=l.content_id where c.owner_id=target and myvoice_private.can_read_content(c.id,false)))
where exists(select 1 from public.profiles p where p.id=target and not myvoice_private.blocked(auth.uid(),target) and(not p.private or target=auth.uid() or exists(select 1 from public.follows where follower=auth.uid() and following=target and accepted)))$$;
revoke all on function myvoice_private.profile_totals(uuid) from public;grant execute on function myvoice_private.profile_totals(uuid) to anon,authenticated;
create function public.profile_totals(target uuid) returns jsonb language sql stable security invoker set search_path='' as $$select myvoice_private.profile_totals(target)$$;
revoke all on function public.profile_totals(uuid) from public;grant execute on function public.profile_totals(uuid) to anon,authenticated;
-- Allow the owner to edit their public handle without allowing reserved names.
grant update(username) on public.profiles to authenticated;
create function myvoice_private.validate_profile_edit() returns trigger language plpgsql set search_path='' as $$begin
 if new.username<>old.username then new.username:=lower(trim(new.username));if new.username in('myvoice','admin','administrator','support','moderator','official','system') then raise exception 'Username reserved';end if;end if;
 if new.avatar is distinct from old.avatar and new.avatar is not null and (length(new.avatar)>90000 or new.avatar not like 'data:image/jpeg;base64,%') then raise exception 'Invalid avatar';end if;
 return new;end$$;
create trigger validate_profile_edit before update on public.profiles for each row execute function myvoice_private.validate_profile_edit();
alter table public.content_versions add column cover_path text;
create or replace function public.package_for_download(target uuid) returns jsonb language sql stable security definer set search_path='' as $$select to_jsonb(v) from public.content_versions v where v.content_id=target and myvoice_private.can_read_content(target,true) order by created_at desc limit 1$$;
