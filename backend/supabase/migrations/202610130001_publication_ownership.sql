alter table public.content add column category text not null default 'Other'
 check(category in ('Meme','SFX','Gaming','Anime','Reaction','Music','Voice','Ambient','Movie/TV','Other'));

-- Private provenance claims prevent a second account from republishing an
-- identical audio asset merely by renaming/repacking its archive.
create table myvoice_private.asset_owners(
 sha256 text primary key check(sha256 ~ '^[a-f0-9]{64}$'),
 owner_id uuid not null references public.profiles(id) on delete cascade
);
revoke all on myvoice_private.asset_owners from public,anon,authenticated;
create function public.claim_publication_assets(p_owner uuid,p_hashes text[])
returns void language plpgsql security definer set search_path='' as $$
declare fingerprint text;
begin
 if cardinality(p_hashes)>200 then raise exception 'Too many assets'; end if;
 -- Stable ordering avoids deadlocks between concurrently submitted boards.
 for fingerprint in select distinct lower(x) from unnest(p_hashes) x order by 1 loop
  insert into myvoice_private.asset_owners values(fingerprint,p_owner) on conflict do nothing;
  if not exists(select 1 from myvoice_private.asset_owners where sha256=fingerprint and owner_id=p_owner)
   then raise exception 'Asset already belongs to another creator'; end if;
 end loop;
end $$;
revoke all on function public.claim_publication_assets(uuid,text[]) from public,anon,authenticated;
grant execute on function public.claim_publication_assets(uuid,text[]) to service_role;
