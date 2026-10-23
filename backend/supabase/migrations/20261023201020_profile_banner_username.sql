-- Owner-editable banner, with the same visibility boundary as the profile bio.
alter table public.profiles add column banner text check (banner is null or (length(banner)<=180000 and banner ~ '^data:image/jpeg;base64,[A-Za-z0-9+/]+=*$'));
grant update(banner) on public.profiles to authenticated;
create function myvoice_private.username_available(handle text) returns boolean language sql stable security definer set search_path='' as $$
 select coalesce(lower(trim(handle)) ~ '^[a-z0-9_]{3,24}$' and lower(trim(handle)) not in ('myvoice','admin','administrator','support','moderator','official','system') and not exists(select 1 from public.profiles where username=lower(trim(handle)) and id is distinct from auth.uid()),false)
$$;
create function public.username_available(handle text) returns boolean language sql stable security invoker set search_path='' as $$select myvoice_private.username_available(handle)$$;
revoke all on function myvoice_private.username_available(text),public.username_available(text) from public,anon,authenticated;
grant execute on function myvoice_private.username_available(text),public.username_available(text) to anon,authenticated;
create function myvoice_private.creator_profile(handle text) returns jsonb language plpgsql stable security definer set search_path='' as $$
declare p public.profiles; is_following boolean; is_reverse boolean; visible boolean;
begin
 select * into p from public.profiles where username=lower(trim(handle));if not found or myvoice_private.blocked(auth.uid(),p.id) then return null;end if;
 is_following:=exists(select 1 from public.follows where follower=auth.uid() and following=p.id and accepted);
 is_reverse:=exists(select 1 from public.follows where follower=p.id and following=auth.uid() and accepted);
 visible:=not p.private or p.id=auth.uid() or is_following;
 return jsonb_build_object('id',p.id,'username',p.username,'display_name',p.display_name,'private',p.private,'verified',p.verified,'bio',case when visible then p.bio else '' end,'avatar',case when visible then p.avatar else null end,'banner',case when visible then p.banner else null end,'relationship',case when is_following and is_reverse then 'Friends' when is_following then 'Following' when exists(select 1 from public.follows where follower=auth.uid() and following=p.id) then 'Requested' when p.private then 'Request Follow' when is_reverse then 'Follow Back' else 'Follow' end);
end$$;
create or replace function public.creator_by_username(handle text) returns jsonb language sql stable security invoker set search_path='' as $$select myvoice_private.creator_profile(handle)$$;
revoke all on function myvoice_private.creator_profile(text),public.creator_by_username(text) from public,anon,authenticated;
grant execute on function myvoice_private.creator_profile(text),public.creator_by_username(text) to anon,authenticated;
