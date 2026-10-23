-- Discord's display name is not a MyVoice handle. Keep email registration rules,
-- and assign a collision-safe local handle only for the trusted OAuth provider.
create or replace function myvoice_private.new_profile()
returns trigger language plpgsql security definer set search_path='' as $$
declare
 handle text:=lower(trim(new.raw_user_meta_data->>'username'));
 label text:=trim(new.raw_user_meta_data->>'display_name');
 attempts integer:=0;
begin
 if new.raw_app_meta_data->>'provider'='discord' then
  label:=left(coalesce(nullif(label,''),nullif(trim(new.raw_user_meta_data->>'full_name'),''),nullif(trim(new.raw_user_meta_data->>'name'),''),'Créateur MyVoice'),60);
  loop
   handle:='user_'||left(replace(gen_random_uuid()::text,'-',''),19);
   begin
    insert into public.profiles(id,username,display_name) values(new.id,handle,label);
    exit;
   exception when unique_violation then
    attempts:=attempts+1;if attempts>=5 then raise;end if;
   end;
  end loop;
 else
  if handle is null or handle !~ '^[a-z0-9_]{3,24}$' or handle in('myvoice','admin','administrator','support','moderator','official','system') then raise exception 'Username unavailable';end if;
  insert into public.profiles(id,username,display_name) values(new.id,handle,coalesce(nullif(label,''),handle));
 end if;
 return new;
end$$;
revoke all on function myvoice_private.new_profile() from public,anon,authenticated;
