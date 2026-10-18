"""Build bounded keyboard packs from licensed recordings, not synthesized clicks.
Run with a source folder containing red.mp3, blue.mp3, clear.mp3,
whitefox.mp3 and ibm.ogg, and an output directory. See SOURCES.json.
"""
import sys,json,hashlib
from pathlib import Path
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly,find_peaks,butter,sosfilt
from math import gcd
source=Path(sys.argv[1]);output=Path(sys.argv[2]);output.mkdir(parents=True,exist_ok=True)
profiles=[('red','Red · Gateron','Linear','#DE5864','SamsterBirdies','489422'),('blue','Blue · Cherry MX','Clicky','#609DE9','SamsterBirdies','489424'),('clear','Clear · Cherry MX','Tactile','#D8E2EF','humi74','412926'),('whitefox','WhiteFox · Hako Violet','Tactile','#B6A0E5','grcekh','546167'),('ibm','IBM M2','Buckling spring','#C4BAA5','Anonimski',None)]
provenance=[]
for ident,name,kind,color,author,number in profiles:
 file=source/(ident+('.mp3' if number else '.ogg'));x,sr=sf.read(file);x=x.mean(axis=1) if x.ndim>1 else x
 g=gcd(sr,48000);x=resample_poly(x,48000//g,sr//g);sr=48000
 x=sosfilt(butter(2,70,fs=sr,btype='highpass',output='sos'),x)
 env=np.sqrt(np.convolve(x*x,np.ones(96)/96,'same'));peaks,_=find_peaks(env,height=env.max()*.08,distance=2880,prominence=env.max()*.05)
 # The WhiteFox recording ends with isolated keys; prefer those over the typing run.
 if ident=='whitefox':peaks=peaks[peaks>sr*145]
 pairs=[]
 for i in range(len(peaks)-1):
  p,q=peaks[i:i+2];gap=(q-p)/sr
  if .07<gap<.28 and env[q]<env[p]*.85 and (i==0 or (p-peaks[i-1])/sr>.12):pairs.append((int(p),int(q)))
 if len(pairs)<8:
  pairs=[(int(p),int(q)) for p,q in zip(peaks[:-1],peaks[1:]) if .065<(q-p)/sr<.25 and env[q]<env[p]]
 assert len(pairs)>=8,(ident,len(pairs))
 pairs=pairs[:32];folder=output/ident;folder.mkdir(exist_ok=True);mapping={};cuts=[]
 for ci,category in enumerate(['normal','space','enter','backspace','shift','modifier','small']):
  for phase,count in [('down',8),('up',4)]:
   files=[]
   for vi in range(count):
    p,q=pairs[(ci*3+vi)%len(pairs)];peak=p if phase=='down' else q
    start=max(0,peak-480);end=min(len(x),peak+7200)
    if phase=='down':end=min(end,q-400)
    else:
     following=peaks[peaks>q+100];end=min(end,int(following[0])-400) if len(following) else end
    clip=x[start:end].copy();assert len(clip)>500
    # Keep the recorded impact/resonance. Large-key colour is an explicit adaptation,
    # because typing recordings don't label every physical key.
    if category in ['space','enter','shift']:
     ratio={'space':.88,'enter':.95,'shift':.97}[category];clip=np.interp(np.arange(0,len(clip),ratio),np.arange(len(clip)),clip)
     if category=='space':clip=sosfilt(butter(1,5500,fs=sr,output='sos'),clip)
    clip-=np.mean(clip);clip*=.52/max(abs(clip).max(),.00001);clip*=.58 if phase=='up' else 1
    fade=min(192,len(clip)//5);clip[:48]*=np.linspace(0,1,48);clip[-fade:]*=np.linspace(1,0,fade)
    relative=f'{category}/{phase}-{vi+1:02}.wav';dest=folder/relative;dest.parent.mkdir(exist_ok=True);sf.write(dest,clip,sr,subtype='PCM_16');files.append(relative);cuts.append({'file':relative,'start':round(start/sr,5),'end':round(end/sr,5)})
   mapping[f'{category}/{phase}']=files
 (folder/'manifest.json').write_text(json.dumps(dict(Id=ident,Name=name,Type=kind,Color=color,Author=author,Version='2.0',DefaultVolume=.7,Samples=mapping),indent=2),encoding='utf-8')
 url=f'https://freesound.org/people/{author}/sounds/{number}/' if number else 'https://commons.wikimedia.org/wiki/File:IBM_M2_sound.ogg'
 provenance.append(dict(id=ident,author=author,source=url,license='CC0-1.0',sourceSha256=hashlib.sha256(file.read_bytes()).hexdigest(),processing='Mono 48 kHz, impact pair segmentation, DC/high-pass cleanup, gain and fades. Large-key variants are adapted recorded impacts, not separately identified physical keys.',cuts=cuts))
 print(ident,len(pairs),'pairs',sum(p.stat().st_size for p in folder.rglob('*.wav')),'bytes')
(output/'SOURCES.json').write_text(json.dumps(provenance,indent=2),encoding='utf-8')
(output/'LICENSE.txt').write_text('Audio recordings: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/\nSources, authors, hashes and edit boundaries are listed in SOURCES.json.\nMyVoice edits: segmented impacts, normalization, fades and large-key colouring. No commercial-app samples.\nThe switch/keyboard names describe the recorded source; no endorsement is implied.\n',encoding='utf-8')
