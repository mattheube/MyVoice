# SPDX-License-Identifier: GPL-3.0-only
"""Seed-VC V2 adapter: real style transfer, pooled identity, bounded reference bank."""
import os,sys,time,json,base64,tempfile,traceback
from pathlib import Path

def run(args,reply):
    import torch, numpy as np, librosa, soundfile as sf, yaml
    from hydra.utils import instantiate
    from omegaconf import DictConfig
    device=torch.device('cuda' if args.device!='cpu' and torch.cuda.is_available() else 'cpu')
    if args.device=='cuda' and device.type!='cuda':raise RuntimeError('CUDA indisponible')
    torch.set_num_threads(max(1,min(args.threads,os.cpu_count() or 1)))
    dtype=torch.float16 if device.type=='cuda' else torch.float32
    model=instantiate(DictConfig(yaml.safe_load(Path('configs/v2/vc_wrapper.yaml').read_text())))
    model.load_checkpoints();model.to(device).eval()
    model.setup_ar_caches(max_batch_size=1,max_seq_len=4096,dtype=dtype,device=device)
    original_style=model.compute_style
    collected=[];wave_chunks=model._stream_wave_chunks
    # Consume the PCM chunks directly; no MP3 encoding/decoding between model and mixer.
    def collect(*values):
        result=wave_chunks(*values[:-1],False);collected[:]=values[4];return result
    model._stream_wave_chunks=collect
    bank=[];pooled=None;last_choice=None
    workspace=tempfile.TemporaryDirectory(prefix='MyVoice-V2-')
    root=Path(workspace.name)
    def signature(audio,sr):
        audio=librosa.resample(audio,orig_sr=sr,target_sr=16000) if sr!=16000 else audio
        energy=librosa.feature.rms(y=audio,frame_length=1024,hop_length=320)[0]
        pitch=librosa.yin(audio,fmin=65,fmax=500,sr=16000,frame_length=1024,hop_length=320)
        voiced=pitch[energy[:len(pitch)]>max(.003,float(np.max(energy))*.08)]
        variation=float(np.std(np.log(np.maximum(voiced,1)))) if len(voiced)>3 else 0.
        dynamic=float(np.std(energy)/(np.mean(energy)+1e-6))
        return np.array([variation,dynamic*.2],dtype=np.float32)
    @torch.inference_mode()
    def load(request):
        nonlocal pooled,last_choice
        bank.clear();last_choice=None;styles=[]
        paths=list(dict.fromkeys(request.get('references') or [request['path']]))
        if len(paths)>64:paths=[paths[int(i)] for i in np.linspace(0,len(paths)-1,64)]
        for index,path in enumerate(paths):
            audio,_=librosa.load(path,sr=16000,mono=True,duration=25)
            if len(audio)<16000 or np.max(np.abs(audio))<.0005:continue
            cached=root/f'reference-{index}.wav';sf.write(cached,audio,16000,subtype='FLOAT')
            bank.append((str(cached),signature(audio,16000)))
            styles.append(original_style(torch.from_numpy(audio).to(device)[None]))
        if not bank:raise ValueError('Aucune référence vocale exploitable')
        pooled=torch.stack(styles).mean(dim=0)
        model.compute_style=lambda *unused,**kwargs:pooled
    @torch.inference_mode()
    def convert(audio,request):
        nonlocal last_choice
        if not bank:raise ValueError('Choisissez une voix')
        if len(audio)==0 or np.max(np.abs(audio))<.0002:return np.zeros(len(audio),dtype=np.float32)
        expression=request.get('expression','adaptive')
        centroid=np.median(np.stack([b[1] for b in bank]),axis=0)
        target=(signature(audio,48000)+centroid)*.5 if expression=='adaptive' else centroid
        distances=[float(np.linalg.norm(b[1]-target)) for b in bank];choice=int(np.argmin(distances))
        # Hysteresis keeps neighboring phrases from randomly changing reference.
        if last_choice is not None and distances[last_choice]<=distances[choice]+.03:choice=last_choice
        last_choice=choice
        source=root/'source.wav';sf.write(source,audio,48000,subtype='FLOAT');collected.clear()
        torch.manual_seed(2026)
        for _ in model.convert_voice_with_streaming(str(source),bank[choice][0],diffusion_steps=int(request.get('steps',30)),similarity_cfg_rate=float(request.get('similarity',.85)),intelligebility_cfg_rate=.7,convert_style=expression!='source',temperature=.7,top_p=.9,repetition_penalty=1.1,device=device,dtype=dtype,stream_output=False):pass
        if not collected:raise RuntimeError('Le modèle n’a produit aucun audio')
        result=librosa.resample(np.concatenate(collected),orig_sr=model.sr,target_sr=48000).astype(np.float32)
        if not np.all(np.isfinite(result)):raise RuntimeError('Audio invalide produit par le modèle')
        if len(result)<min(4800,len(audio)*.25) or np.max(np.abs(result))<.0001:raise ValueError('Le modèle n’a pas produit une phrase exploitable. Essayez une phrase plus longue ou le mode Conserver mon intonation.')
        # Gentle phrase boundaries; never warp output to force the original rhythm.
        if request.get('artifacts',True):
            fade=min(480,len(result)//2);result[:fade]*=np.linspace(0,1,fade);result[-fade:]*=np.linspace(1,0,fade)
        if expression=='source' and request.get('preserve',True):
            from streaming import shape_dynamics
            if len(result)<len(audio):audio=audio[:len(result)]
            elif len(result)>len(audio):audio=np.pad(audio,(0,len(result)-len(audio)))
            result=shape_dynamics(result,audio,True)
        peak=float(np.max(np.abs(result)));result*=min(1.,.98/max(peak,1e-6))
        return result
    reply({'state':'ready','gpu':torch.cuda.get_device_name(0) if device.type=='cuda' else 'CPU','model':'Seed-VC V2'})
    try:
        for line in sys.stdin:
            start=time.perf_counter()
            try:
                request=json.loads(line);command=request['command']
                if command=='stop':break
                if command=='load':
                    load(request);reply({'state':'loaded','seconds':time.perf_counter()-start,'references':len(bank),'vram_mb':torch.cuda.memory_allocated()/1048576 if device.type=='cuda' else 0});continue
                if command=='chunk':
                    audio=np.frombuffer(base64.b64decode(request['audio']),dtype='<f4').copy();result=convert(audio,request)
                    reply({'audio':base64.b64encode(result.astype('<f4').tobytes()).decode(),'seconds':time.perf_counter()-start});continue
                if command=='file':
                    audio,_=librosa.load(request['input'],sr=48000,mono=True);pieces=[]
                    # Balance chunks so a long file never ends with an unusably short tail.
                    chunks=max(1,(len(audio)+48000*12-1)//(48000*12))
                    for index,part in enumerate(np.array_split(audio,chunks)):
                        if len(part)==0:continue
                        pieces.append(convert(part,request));reply({'progress':(index+1)/chunks})
                    if not pieces:raise ValueError('Fichier vide')
                    result=np.concatenate(pieces);sf.write(request['output'],result,48000,subtype='PCM_16');reply({'state':'converted','seconds':time.perf_counter()-start});continue
                raise ValueError('Commande inconnue')
            except Exception as error:traceback.print_exc(file=sys.stderr);reply({'error':str(error)})
    finally:workspace.cleanup()

