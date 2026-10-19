create or replace function myvoice_private.profile_activity(list_kind text) returns jsonb language plpgsql stable security definer set search_path='' as $$
declare result jsonb;
begin
 if auth.uid() is null then raise exception 'Sign in first';end if;
 if list_kind not in ('followers','following','likes') or list_kind is null then raise exception 'Invalid list';end if;
 if list_kind='likes' then
  select coalesce(jsonb_agg(to_jsonb(row)),'[]'::jsonb) into result from (
   select p.id,p.display_name as name,p.username,c.title as detail
   from public.content c join public.likes l on l.content_id=c.id join public.profiles p on p.id=l.user_id
   where c.owner_id=auth.uid() and myvoice_private.can_read_content(c.id,false) and not myvoice_private.blocked(auth.uid(),p.id) and (p.id=auth.uid() or not p.private or exists(select 1 from public.follows visible where visible.follower=auth.uid() and visible.following=p.id and visible.accepted))
   order by c.created_at desc,c.id,p.id limit 50
  ) row;
 else
  select coalesce(jsonb_agg(to_jsonb(row)),'[]'::jsonb) into result from (
   select p.id,p.display_name as name,p.username,''::text as detail
   from public.follows f join public.profiles p on p.id=case when list_kind='followers' then f.follower else f.following end
   where f.accepted and case when list_kind='followers' then f.following=auth.uid() else f.follower=auth.uid() end
   and not myvoice_private.blocked(auth.uid(),p.id) and (p.id=auth.uid() or not p.private or exists(select 1 from public.follows visible where visible.follower=auth.uid() and visible.following=p.id and visible.accepted))
   order by f.created_at desc,p.id limit 50
  ) row;
 end if;
 return result;
end$$;
