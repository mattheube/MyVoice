"""Pinned, checked model assets. Provisioning does not import torch or load weights."""
import argparse,hashlib,json,os
from pathlib import Path

LOCK=Path(__file__).with_name("models.lock.json")

def catalog():
    return json.loads(LOCK.read_text(encoding="utf-8"))["models"]

def cache_for(model,repository):
    from huggingface_hub.constants import HF_HUB_CACHE
    return str(Path(repository)/"checkpoints"/"hf_cache") if model["cache"]=="semantic" else str(Path(repository)/"checkpoints") if model["cache"]=="repo" else HF_HUB_CACHE

def install_pins(repository="."):
    """Apply before importing model libraries; every supported repo resolves a fixed revision/cache."""
    os.environ["HF_HUB_OFFLINE"]="1"
    os.environ["TRANSFORMERS_OFFLINE"]="1"
    import huggingface_hub as hub
    import huggingface_hub.file_download as download
    models={m["repo"]:m for m in catalog()}
    original=hub.hf_hub_download
    def pinned(repo_id,filename,*args,**kwargs):
        if repo_id in models:
            model=models[repo_id]
            kwargs.update(revision=model["revision"],cache_dir=cache_for(model,repository),local_files_only=True)
        return original(repo_id,filename,*args,**kwargs)
    hub.hf_hub_download=pinned
    download.hf_hub_download=pinned

def valid(path,entry,prior,force):
    if not path.is_file():return False
    stat=path.stat()
    if stat.st_size!=entry["size"]:return False
    if not force and prior==[stat.st_size,stat.st_mtime_ns,entry["sha256"]]:return True
    with path.open("rb") as stream:
        return hashlib.file_digest(stream,"sha256").hexdigest()==entry["sha256"]

def provision(repository,state,group="all",force=False):
    from huggingface_hub import hf_hub_download
    receipt=Path(state);receipt.parent.mkdir(parents=True,exist_ok=True)
    try:previous=json.loads(receipt.read_text(encoding="utf-8")).get("files",{})
    except (ValueError,OSError):previous={}
    checked={}
    selected=[m for m in catalog() if group=="all" or (m["repo"]=="ResembleAI/chatterbox")== (group=="tts")]
    total=sum(len(m["files"]) for m in selected);index=0
    for model in selected:
        cache=cache_for(model,repository)
        for entry in model["files"]:
            index+=1
            print(f"CHECKING · {index}/{total} · {model['repo']} / {entry['path']}",flush=True)
            args=dict(repo_id=model["repo"],filename=entry["path"],revision=model["revision"],cache_dir=cache)
            try:path=Path(hf_hub_download(**args,local_files_only=True))
            except Exception:path=Path(cache)/".missing-model"
            if not valid(path,entry,previous.get(str(path)),force):
                print(f"DOWNLOADING · {index}/{total} · {entry['path']}",flush=True)
                path=Path(hf_hub_download(**args,force_download=path.is_file()))
                if not valid(path,entry,None,True):raise ValueError("Empreinte de modèle invalide : "+entry["path"])
            stat=path.stat();checked[str(path)]=[stat.st_size,stat.st_mtime_ns,entry["sha256"]]
    result={"catalog_sha256":hashlib.sha256(LOCK.read_bytes()).hexdigest(),"group":group,"files":checked,
            "status":"downloaded_verified_not_loaded"}
    temporary=receipt.with_suffix(".tmp")
    temporary.write_text(json.dumps(result,indent=2),encoding="utf-8")
    os.replace(temporary,receipt)
    print("VERIFIED · Modèles disponibles sur disque ; chargement uniquement à l’utilisation.",flush=True)

if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("--repository",required=True);p.add_argument("--state",required=True)
    p.add_argument("--group",choices=["seed","tts","all"],default="all");p.add_argument("--verify",action="store_true")
    a=p.parse_args();provision(a.repository,a.state,a.group,a.verify)
