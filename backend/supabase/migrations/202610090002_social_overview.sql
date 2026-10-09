create function public.social_overview() returns jsonb language plpgsql stable security definer set search_path='' as $$
begin
 if auth.uid() is null then raise exception 'Sign in first';end if;
 return jsonb_build_object(
 'friends',coalesce((select jsonb_agg(jsonb_build_object('id',p.id,'name',p.display_name,'username',p.username,'kind','friend')) from public.profiles p where myvoice_private.friends(auth.uid(),p.id)),'[]'::jsonb),
 'requests',coalesce((select jsonb_agg(jsonb_build_object('id',p.id,'name',p.display_name,'username',p.username,'kind','request')) from public.follows f join public.profiles p on p.id=f.follower where f.following=auth.uid() and not f.accepted and not myvoice_private.blocked(auth.uid(),p.id)),'[]'::jsonb),
 'following',(select count(*) from public.follows where follower=auth.uid() and accepted),
 'followers',(select count(*) from public.follows where following=auth.uid() and accepted));
end$$;
create function public.moderation_overview() returns jsonb language plpgsql stable security definer set search_path='' as $$
begin
 if not exists(select 1 from myvoice_private.roles where user_id=auth.uid() and role in('admin','moderator') and not suspended)then raise exception 'Forbidden';end if;
 return coalesce((select jsonb_agg(to_jsonb(r)) from(select r.id,r.content_id,r.reason,r.details,c.title from public.reports r left join public.content c on c.id=r.content_id order by r.created_at desc limit 100)r),'[]'::jsonb);
end$$;
create function public.admin_creator(target uuid,action text) returns void language plpgsql security definer set search_path='' as $$
begin
 if not exists(select 1 from myvoice_private.roles where user_id=auth.uid() and role='admin' and not suspended)then raise exception 'Forbidden';end if;
 if target=auth.uid() then raise exception 'Use server console for own role';end if;
 if action='verify' then update public.profiles set verified=true where id=target;
 elsif action='unverify' then update public.profiles set verified=false where id=target;
 elsif action='suspend' then insert into myvoice_private.roles(user_id,role,suspended)values(target,'user',true)on conflict(user_id)do update set suspended=true;
 elsif action='restore' then update myvoice_private.roles set suspended=false where user_id=target;
 else raise exception 'Invalid action';end if;
end$$;
revoke all on function public.social_overview(),public.moderation_overview(),public.admin_creator(uuid,text) from public,anon;
grant execute on function public.social_overview(),public.moderation_overview(),public.admin_creator(uuid,text) to authenticated;
