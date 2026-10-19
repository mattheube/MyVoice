create function myvoice_private.profile_activity(list_kind text) returns jsonb language plpgsql stable security definer set search_path='' as $$
declare result jsonb;
begin
 if auth.uid() is null then raise exception 'Sign in first';end if;
 if list_kind not in ('followers','following','likes') or list_kind is null then raise exception 'Invalid list';end if;
 if list_kind='likes' then
  select coalesce(jsonb_agg(to_jsonb(row)),'[]'::jsonb) into result from (
   select p.id,p.display_name as name,p.username,c.title as detail
   from public.content c join public.likes l on l.content_id=c.id join public.profiles p on p.id=l.user_id
   where c.owner_id=auth.uid() and myvoice_private.can_read_content(c.id,false) and not myvoice_private.blocked(auth.uid(),p.id)
   order by c.created_at desc,c.id,p.id limit 50
  ) row;
 else
  select coalesce(jsonb_agg(to_jsonb(row)),'[]'::jsonb) into result from (
   select p.id,p.display_name as name,p.username,''::text as detail
   from public.follows f join public.profiles p on p.id=case when list_kind='followers' then f.follower else f.following end
   where f.accepted and case when list_kind='followers' then f.following=auth.uid() else f.follower=auth.uid() end
   and not myvoice_private.blocked(auth.uid(),p.id)
   order by f.created_at desc,p.id limit 50
  ) row;
 end if;
 return result;
end$$;
revoke all on function myvoice_private.profile_activity(text) from public,anon;
grant execute on function myvoice_private.profile_activity(text) to authenticated;
create function public.profile_activity(list_kind text) returns jsonb language sql stable security invoker set search_path='' as $$select myvoice_private.profile_activity(list_kind)$$;
revoke all on function public.profile_activity(text) from public,anon;
grant execute on function public.profile_activity(text) to authenticated;

create index if not exists content_owner_activity_idx on public.content(owner_id,created_at desc,id);
create index if not exists likes_content_activity_idx on public.likes(content_id,user_id);
