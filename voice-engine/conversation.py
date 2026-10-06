# SPDX-License-Identifier: GPL-3.0-only
"""Bounded-lookahead V2 timbre streaming; no autoregressive style generation."""
import os,sys,time,json,base64,traceback
from pathlib import Path
import numpy as np

class RollingJoin:
    def __init__(self,overlap):
        self.n=overlap;self.tail=np.zeros(overlap,dtype=np.float32)
    def push(self,rendered,count):
        need=count+self.n
        if len(rendered)<need:rendered=np.pad(rendered,(need-len(rendered),0))
        current=rendered[-need:];w=(.5-.5*np.cos(np.linspace(0,np.pi,self.n))).astype(np.float32)
        out=np.concatenate([self.tail*(1-w)+current[:self.n]*w,current[self.n:-self.n]])
        self.tail=current[-self.n:].copy();return out.astype(np.float32)

class SpeechMask:
    """WebRTC speech decisions, 180 ms pre-roll, 240 ms breathing/hangover."""
    def __init__(self):
        try:import webrtcvad
        except ImportError:raise RuntimeError('Conversation nécessite WebRTC VAD. Lancez Installer / réparer le moteur dans Voice Lab.')
        self.vad=webrtcvad.Vad(1)
    def mask(self,audio):
        # Full rolling context primes VAD every pass; avoids dependence on call scheduling.
        self.vad.set_mode(1);flags=[]
        for start in range(0,len(audio),960):
            frame=audio[start:start+960];padded=np.pad(frame,(0,960-len(frame)))
            pcm=(np.clip(padded,-1,1)*32767).astype('<i2').tobytes()
            flags.append(self.vad.is_speech(pcm,48000) and np.sqrt(np.mean(padded*padded))>.0003)
        expanded=np.zeros(len(flags),np.float32)
        for i,on in enumerate(flags):
            if on:expanded[max(0,i-9):min(len(flags),i+13)]=1
        points=np.arange(len(flags))*960+480
        return np.interp(np.arange(len(audio)),points,expanded).astype(np.float32),any(flags[-max(1,12):])

def run(args,reply):
    import torch,torchaudio,librosa,soundfile as sf,yaml
    from hydra.utils import instantiate
    from omegaconf import DictConfig
    device=torch.device('cuda' if args.device!='cpu' and torch.cuda.is_available() else 'cpu')
    if args.device=='cuda' and device.type!='cuda':raise RuntimeError('CUDA indisponible')
    torch.set_num_threads(max(1,min(args.threads,os.cpu_count() or 1)));dtype=torch.float16 if device.type=='cuda' else torch.float32
    model=instantiate(DictConfig(yaml.safe_load(Path('configs/v2/vc_wrapper.yaml').read_text())))
    model.load_checkpoints();model.ar=None;model.ar_length_regulator=None;model.content_extractor_narrow=None
    model.to(device).eval();torch.manual_seed(2026)
    prompt=None;profile={};history=np.zeros(0,np.float32);join=None;vad=SpeechMask();silent=0;last_rtf=0
    @torch.inference_mode()
    def render(audio,steps,similarity):
        x=torch.from_numpy(audio.copy())[None].to(device);x=torchaudio.functional.resample(x,48000,22050)
        x16=torchaudio.functional.resample(x,22050,16000)
        with torch.autocast(device.type,dtype=dtype,enabled=device.type=='cuda'):
            tokens=model._process_content_features(x16,is_narrow=False)
            n=model.mel_fn(x).shape[-1]
            cond=model.cfm_length_regulator(tokens,ylens=torch.LongTensor([n]).to(device))[0]
            p,mel,style=prompt;cat=torch.cat([p,cond],dim=1)
            result=model.cfm.inference(cat,torch.LongTensor([cat.size(1)]).to(device),mel,style,steps,inference_cfg_rate=[.7,similarity])
            wave=model.vocoder(result[:,:,mel.shape[-1]:].float()).squeeze()
        out=torchaudio.functional.resample(wave,22050,48000).cpu().numpy()
        return np.pad(out,(0,max(0,len(audio)-len(out))))[:len(audio)].astype(np.float32)
    def reset():
        nonlocal history,join,silent
        history=np.zeros(int(profile['context']*48000),np.float32);join=RollingJoin(int(profile['overlap']*48000));silent=0
    @torch.inference_mode()
    def load(request):
        nonlocal prompt,profile
        paths=list(dict.fromkeys(request.get('references') or [request['path']]))
        if len(paths)>64:paths=[paths[int(i)] for i in np.linspace(0,len(paths)-1,64)]
        styles=[];reference=None
        for path in paths:
            audio,_=librosa.load(path,sr=22050,mono=True,duration=12)
            if len(audio)<22050 or np.max(np.abs(audio))<.0005:continue
            if reference is None:reference=audio[:22050*4]
            x=torch.from_numpy(audio)[None].to(device);styles.append(model.compute_style(torchaudio.functional.resample(x,22050,16000)))
        if reference is None:raise ValueError('Aucune référence exploitable')
        x=torch.from_numpy(reference)[None].to(device)
        with torch.autocast(device.type,dtype=dtype,enabled=device.type=='cuda'):
            mel=model.mel_fn(x);tokens=model._process_content_features(torchaudio.functional.resample(x,22050,16000),is_narrow=False)
            condition=model.cfm_length_regulator(tokens,ylens=torch.LongTensor([mel.shape[-1]]).to(device))[0]
        prompt=(condition,mel,torch.stack(styles).mean(dim=0))
        requested={'Quality':8,'Balanced':6,'Low latency':4}.get(request.get('profile'),6)
        sample=librosa.resample(reference,orig_sr=22050,target_sr=48000)[:81600]
        if len(sample)<81600:sample=np.pad(sample,(0,81600-len(sample)))
        render(sample,2,.85) # warm-up is excluded from recommendation
        times=[]
        for _ in range(2 if device.type=='cuda' else 1):
            start=time.perf_counter();render(sample,requested,.85);times.append(time.perf_counter()-start)
        worst=max(times);hop=min(.96,max(.64,np.ceil(worst*1.30*50)/50))
        profile={'hop':float(hop),'context':.8,'overlap':.16,'lookahead':.12,'steps':requested,'processing':worst,'rtf':worst/hop,'recommended':worst/hop<.85 and hop+.28+worst<2,'references':len(styles)}
        reset();return profile
    def chunk(audio,request):
        nonlocal history,silent,last_rtf
        if request.get('reset'):reset()
        joined=np.concatenate([history,audio]);mask,speaking=vad.mask(joined)
        delay=int(profile['lookahead']*48000);count=len(audio)
        # Silence is never sent to the neural model; breath margins remain in the mask.
        if np.max(mask[-(count+delay+join.n):])==0:
            converted=np.zeros(len(joined),np.float32);silent+=count
        else:
            converted=render(joined,profile['steps'],float(np.clip(request.get('similarity',.85),0,1)))
            converted*=mask
            if request.get('preserve',True):
                from streaming import shape_dynamics
                converted=shape_dynamics(converted,joined,True)
            silent=0
        out=join.push(converted[:-delay],count)
        history=joined[-int(profile['context']*48000):]
        # Adapt only in a speech pause, retaining the same timing to avoid pitch/queue jumps.
        if not speaking and last_rtf>1 and profile['steps']>4:profile['steps']-=1
        if silent>48000*2:history.fill(0);join.tail.fill(0)
        if not np.isfinite(out).all():raise RuntimeError('Audio Conversation invalide')
        return np.clip(out,-.98,.98).astype(np.float32),speaking
    reply({'state':'ready','gpu':torch.cuda.get_device_name(0) if device.type=='cuda' else 'CPU','model':'Seed-VC V2 rolling timbre'})
    for line in sys.stdin:
        try:
            request=json.loads(line);command=request['command'];start=time.perf_counter()
            if command=='stop':break
            if command=='load':
                result=load(request);reply(dict(state='loaded',seconds=time.perf_counter()-start,vram_mb=torch.cuda.memory_allocated()/1048576 if device.type=='cuda' else 0,**result));continue
            if command=='reset':reset();reply({'state':'reset'});continue
            if command=='chunk':
                audio=np.frombuffer(base64.b64decode(request['audio']),dtype='<f4').copy();out,speaking=chunk(audio,request)
                elapsed=time.perf_counter()-start;last_rtf=elapsed/max(.001,len(audio)/48000)
                reply({'audio':base64.b64encode(out.astype('<f4').tobytes()).decode(),'seconds':elapsed,'speech':speaking,'steps':profile['steps']});continue
            if command=='file':
                audio,_=librosa.load(request['input'],sr=48000,mono=True);reset();parts=[];n=int(profile['hop']*48000)
                # Flush look-ahead and overlap at EOF, then remove algorithmic leading delay.
                padded=np.pad(audio,(0,n+int(.28*48000)))
                for offset in range(0,len(padded),n):
                    part=padded[offset:offset+n];part=np.pad(part,(0,n-len(part)));parts.append(chunk(part,request)[0]);reply({'progress':min(1,(offset+n)/len(padded))})
                result=np.concatenate(parts)[13440:13440+len(audio)];sf.write(request['output'],result,48000,subtype='PCM_16');reset()
                reply({'state':'converted','seconds':time.perf_counter()-start});continue
            raise ValueError('Commande inconnue')
        except Exception as exc:traceback.print_exc(file=sys.stderr);reply({'error':str(exc)})
