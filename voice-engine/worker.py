# SPDX-License-Identifier: GPL-3.0-only
"""MyVoice local Seed-VC adapter. JSON lines over private child stdin/stdout; no server."""
import os, sys, json, base64, time, traceback, argparse
parser=argparse.ArgumentParser()
parser.add_argument('--repository', required=True)
parser.add_argument('--device', default='auto')
parser.add_argument('--model', default='fast')
parser.add_argument('--threads', type=int, default=4)
args=parser.parse_args()
protocol=sys.stdout
sys.stdout=sys.stderr
os.chdir(args.repository)
sys.path.insert(0,args.repository)
os.environ['HF_HUB_DISABLE_TELEMETRY']='1'
os.environ['DO_NOT_TRACK']='1'
os.environ['OMP_NUM_THREADS']='4'
os.environ['TQDM_DISABLE']='1'
def reply(data):
    protocol.write(json.dumps(data)+'\n'); protocol.flush()
if args.model=='conversation':
    try:
        from conversation import run
        run(args,reply)
    except Exception as exc:
        traceback.print_exc(file=sys.stderr);reply({'error':str(exc)});sys.exit(1)
    sys.exit(0)
if args.model=='studio':
    try:
        from studio import run
        run(args,reply)
    except Exception as exc:
        traceback.print_exc(file=sys.stderr);reply({'error':str(exc)});sys.exit(1)
    sys.exit(0)
try:
    import torch, torchaudio, numpy as np, librosa, soundfile as sf
    from types import SimpleNamespace
    from streaming import OverlapJoin, shape_dynamics
    import inference
    from hf_utils import load_custom_model_from_hf
    device=torch.device('cuda' if args.device!='cpu' and torch.cuda.is_available() else 'cpu')
    torch.set_num_threads(max(1,min(args.threads,os.cpu_count() or 1)))
    if args.device=='cuda' and not torch.cuda.is_available(): raise ValueError('CUDA unavailable')
    inference.device=device
    checkpoint,config=load_custom_model_from_hf('Plachta/Seed-VC','DiT_uvit_tat_xlsr_ema.pth','config_dit_mel_seed_uvit_xlsr_tiny.yml')
    model,semantic,_,vocoder,style_encoder,mel_fn,mel_args=inference.load_models(SimpleNamespace(checkpoint=checkpoint,config=config,fp16=device.type=='cuda',f0_condition=False))
    sr=mel_args['sampling_rate']
    prompt=None
    loaded_path=None
    stitch=OverlapJoin()
    history=np.zeros(0,dtype=np.float32)
    reply({'state':'ready','device':str(device),'gpu':torch.cuda.get_device_name(0) if device.type=='cuda' else 'CPU'})
    @torch.inference_mode()
    def reference(path):
        global prompt,history,loaded_path,stitch
        stitch=OverlapJoin()
        history=np.zeros(0,dtype=np.float32)
        if loaded_path==path and prompt is not None:return
        wav,_=librosa.load(path,sr=sr,mono=True,duration=12)
        if len(wav)<sr: raise ValueError('Reference must be at least one second')
        x=torch.from_numpy(wav).to(device).unsqueeze(0)
        x16=torchaudio.functional.resample(x,sr,16000)
        semantics=semantic(x16)
        mel=mel_fn(x)
        features=torchaudio.compliance.kaldi.fbank(x16,num_mel_bins=80,dither=0,sample_frequency=16000)
        features=features-features.mean(dim=0,keepdim=True)
        style=style_encoder(features.unsqueeze(0))
        condition=model.length_regulator(semantics,ylens=torch.LongTensor([mel.size(2)]).to(device),n_quantizers=3,f0=None)[0]
        prompt=(condition,mel,style)
        loaded_path=path
        history=np.zeros(0,dtype=np.float32)
    @torch.inference_mode()
    def convert(samples,steps=6,similarity=.7,preserve=True):
        if prompt is None: raise ValueError('Select a reference voice first')
        original=len(samples)
        if original==0: return samples
        if np.max(np.abs(samples))<0.00005: return np.zeros(original,dtype=np.float32)
        x=torch.from_numpy(samples.copy()).float().to(device).unsqueeze(0)
        x=torchaudio.functional.resample(x,48000,sr)
        if x.shape[-1]<sr//2: x=torch.nn.functional.pad(x,(0,sr//2-x.shape[-1]))
        x16=torchaudio.functional.resample(x,sr,16000)
        content=semantic(x16)
        mel=mel_fn(x)
        condition=model.length_regulator(content,ylens=torch.LongTensor([mel.size(2)]).to(device),n_quantizers=3,f0=None)[0]
        p,refmel,style=prompt
        cat=torch.cat([p,condition],dim=1)
        with torch.autocast(device_type=device.type,dtype=torch.float16,enabled=device.type=='cuda'):
            converted=model.cfm.inference(cat,torch.LongTensor([cat.size(1)]).to(device),refmel,style,None,int(steps),inference_cfg_rate=float(np.clip(similarity,0,1))*1.2)
        audio=vocoder(converted[:,:,refmel.size(-1):].float())[0].reshape(-1)
        audio=torchaudio.functional.resample(audio,sr,48000).detach().cpu().numpy()
        if len(audio)<original: audio=np.pad(audio,(0,original-len(audio)))
        audio=shape_dynamics(audio[:original],samples,preserve)
        wet=min(1.,max(0.,similarity)/.7)
        return np.clip(audio*wet+samples[:original]*(1-wet),-.98,.98).astype(np.float32)
    for line in sys.stdin:
        try:
            request=json.loads(line); command=request['command']; start=time.perf_counter()
            if command=='stop': break
            if command=='load':
                reference(request['path'])
                # Run a real model pass to initialize kernels before enabling the live path.
                convert(np.sin(np.arange(38400,dtype=np.float32)*.028)*.01,2)
                reply({'state':'loaded','seconds':time.perf_counter()-start,'vram_mb':torch.cuda.memory_allocated()/1048576 if device.type=='cuda' else 0});continue
            if command=='chunk':
                data=np.frombuffer(base64.b64decode(request['audio']),dtype='<f4').copy()
                joined=np.concatenate([history,data])
                result=convert(joined,request.get('steps',6),request.get('similarity',.7),request.get('preserve',True))
                result=stitch.push(result,len(data)) if request.get('artifacts',True) else result[-len(data):]
                history=joined[-48000:]
                reply({'audio':base64.b64encode(result.astype('<f4').tobytes()).decode(),'seconds':time.perf_counter()-start});continue
            if command=='file':
                data,_=librosa.load(request['input'],sr=48000,mono=True)
                pieces=[]
                file_stitch=OverlapJoin()
                for begin in range(0,len(data),48000*5):
                    left=max(0,begin-12000);right=min(len(data),begin+48000*5)
                    converted=convert(data[left:right],request.get('steps',10),request.get('similarity',.7),request.get('preserve',True))
                    pieces.append(file_stitch.push(converted,right-begin))
                    reply({'progress':right/max(1,len(data))})
                if not pieces: raise ValueError('Empty audio file')
                output=np.concatenate(pieces+[file_stitch.tail])[960:960+len(data)]
                sf.write(request['output'],output,48000,subtype='PCM_16')
                reply({'state':'converted','seconds':time.perf_counter()-start,'duration':len(output)/48000});continue
            raise ValueError('Unknown command')
        except Exception as exc:
            traceback.print_exc(file=sys.stderr);reply({'error':str(exc)})
except Exception as exc:
    traceback.print_exc(file=sys.stderr);reply({'error':str(exc)});sys.exit(1)
