alter table public.content add column audio_locked boolean not null default false;
alter table public.content_versions add column preview_path text;
update storage.buckets set allowed_mime_types=array['application/zip','image/jpeg','image/png','image/gif','image/webp','audio/wav'] where id='community';
update public.content set audio_locked=true where status='published' and visibility<>'private';

create function myvoice_private.lock_shared_audio() returns trigger language plpgsql set search_path='' as $$
begin
 if tg_op='UPDATE' and old.audio_locked then new.audio_locked:=true;end if;
 if new.status='published' and new.visibility<>'private' then new.audio_locked:=true;end if;
 return new;
end$$;
create trigger lock_shared_audio before insert or update on public.content for each row execute function myvoice_private.lock_shared_audio();

create function myvoice_private.protect_audio_version() returns trigger language plpgsql set search_path='' as $$
declare target uuid;locked boolean;
begin
 target:=case when tg_op='DELETE' then old.content_id else new.content_id end;
 select audio_locked into locked from public.content where id=target for update;
 if tg_op='UPDATE' and new.content_id<>old.content_id then raise exception 'Cannot move a publication version';end if;
 if locked and (
  tg_op='DELETE' or
  (tg_op='INSERT' and exists(select 1 from public.content_versions where content_id=target)) or
  (tg_op='UPDATE' and (new.object_path,new.sha256,new.size,new.version) is distinct from (old.object_path,old.sha256,old.size,old.version))
 ) then raise exception 'Shared audio is permanently locked';end if;
 if tg_op='DELETE' then return old;end if;return new;
end$$;
create trigger protect_audio_version before insert or update or delete on public.content_versions for each row execute function myvoice_private.protect_audio_version();
revoke all on function myvoice_private.lock_shared_audio(),myvoice_private.protect_audio_version() from public,anon,authenticated;

create function myvoice_private.edit_publication(
 target uuid,new_title text,new_description text,new_tags text[],new_category text,new_language text,new_visibility text,new_cover text default null,new_preview text default null
) returns jsonb language plpgsql security definer set search_path='' as $$
declare row_data public.content;prefix text;
begin
 if auth.uid() is null then raise exception 'Sign in first';end if;
 perform myvoice_private.rate_limit('edit_publication',20);
 select * into row_data from public.content where id=target and owner_id=auth.uid() and status='published' for update;
 if not found then raise exception 'Publication unavailable';end if;
 if new_title is null or length(trim(new_title)) not between 1 and 100 or new_description is null or length(new_description)>2000
  or new_tags is null or cardinality(new_tags)>8 or exists(select 1 from unnest(new_tags) t where t is null or length(t)>30)
  or new_category is null or new_category not in('Meme','SFX','Gaming','Anime','Reaction','Music','Voice','Ambient','Movie/TV','Other')
  or new_language is null or length(new_language) not between 1 and 12 or new_visibility is null or new_visibility not in('private','public','friends','unlisted') then raise exception 'Invalid metadata';end if;
 prefix:=auth.uid()::text||'/'||target::text||'/';
 if new_cover is not null and (left(new_cover,length(prefix))<>prefix or substring(new_cover from length(prefix)+1) !~ '^cover-[a-f0-9]{32}\.jpg$') then raise exception 'Invalid cover path';end if;
 if new_preview is not null and (left(new_preview,length(prefix))<>prefix or substring(new_preview from length(prefix)+1) !~ '^preview-[a-f0-9]{32}\.wav$') then raise exception 'Invalid preview path';end if;
 update public.content set title=trim(new_title),description=new_description,tags=new_tags,category=new_category,language=new_language,visibility=new_visibility where id=target returning * into row_data;
 if new_cover is not null or new_preview is not null then
  update public.content_versions set cover_path=coalesce(new_cover,cover_path),preview_path=coalesce(new_preview,preview_path)
   where id=(select id from public.content_versions where content_id=target order by created_at desc,id desc limit 1);
 end if;
 return to_jsonb(row_data);
end$$;
revoke all on function myvoice_private.edit_publication(uuid,text,text,text[],text,text,text,text,text) from public;
grant execute on function myvoice_private.edit_publication(uuid,text,text,text[],text,text,text,text,text) to authenticated;
create function public.edit_publication(target uuid,new_title text,new_description text,new_tags text[],new_category text,new_language text,new_visibility text,new_cover text default null,new_preview text default null)
returns jsonb language sql security invoker set search_path='' as $$select myvoice_private.edit_publication(target,new_title,new_description,new_tags,new_category,new_language,new_visibility,new_cover,new_preview)$$;
revoke all on function public.edit_publication(uuid,text,text,text[],text,text,text,text,text) from public;
grant execute on function public.edit_publication(uuid,text,text,text[],text,text,text,text,text) to authenticated;

-- Only the authenticated edge function can commit an uploaded private replacement.
-- INVOKER keeps this internal transaction under the service role's existing rights.
create function public.replace_private_audio(target uuid,actor uuid,expected_hash text,new_object text,new_hash text,new_size bigint,new_version text,new_cover text default null)
returns void language plpgsql security invoker set search_path='' as $$
declare item public.content;latest public.content_versions;
begin
 if actor is null then raise exception 'Owner required';end if;
 select * into item from public.content where id=target and owner_id=actor and status='published' for update;
 if not found or item.audio_locked or item.visibility<>'private' then raise exception 'Audio replacement unavailable';end if;
 select * into latest from public.content_versions where content_id=target order by created_at desc,id desc limit 1 for update;
 if not found or latest.sha256<>expected_hash then raise exception 'Publication changed; refresh first';end if;
 if new_object not like actor::text||'/'||target::text||'/%' or new_object like '%..%' then raise exception 'Invalid object path';end if;
 update public.content_versions set object_path=new_object,sha256=new_hash,size=new_size,version=new_version,cover_path=coalesce(new_cover,cover_path) where id=latest.id;
end$$;
revoke all on function public.replace_private_audio(uuid,uuid,text,text,text,bigint,text,text) from public,anon,authenticated;
grant execute on function public.replace_private_audio(uuid,uuid,text,text,text,bigint,text,text) to service_role;
