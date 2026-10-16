from pathlib import Path
import json,math,random,wave,struct
root=Path('outputs/MyVoice/assets/KeyboardPacks');root.mkdir(exist_ok=True)
profiles=[('red','Red','Linear','#DE5864',520,.048,.13),('blue','Blue','Clicky','#5A9EE8',1700,.032,.65),('brown','Brown','Tactile','#B38257',850,.043,.28),('black','Black','Heavy linear','#747A86',310,.072,.18),('silver','Silver','Light linear','#B9C6D8',1250,.026,.32),('speed-silver','Speed Silver','Short travel','#E1E4EB',1550,.019,.38),('yellow','Yellow','Rounded linear','#E6C454',650,.060,.12),('green','Green','Heavy clicky','#62AE80',1350,.056,.72),('clear','Clear','Firm tactile','#BCDADC',1000,.050,.36),('silent-red','Silent Red','Damped linear','#D98C97',420,.024,.04)]
categories={'normal':1,'space':.58,'enter':.76,'backspace':.87,'shift':.82,'modifier':.94,'small':1.13}
for index,(pid,name,kind,color,freq,decay,click) in enumerate(profiles):
 folder=root/pid;folder.mkdir(exist_ok=True);samples={}
 for category,ratio in categories.items():
  for edge,count in [('down',8),('up',4)]:
   names=[]
   for variation in range(count):
    rng=random.Random(index*10000+list(categories).index(category)*100+variation+(50 if edge=='up' else 0))
    f=freq*ratio*(.97+.06*rng.random())*(1.14 if edge=='up' else 1);tau=decay*(.68 if edge=='up' else 1)*(1.2 if category=='space' else 1)
    length=int(48000*min(.24,tau*4.5));values=[];noise=0
    for i in range(length):
     t=i/48000;noise=.35*noise+.65*rng.uniform(-1,1);attack=min(1,i/18)
     body=(math.sin(2*math.pi*f*t)*.55+math.sin(2*math.pi*f*2.37*t)*.19)*math.exp(-t/tau)
     impact=noise*math.exp(-t/.006)*(click+.12)
     second=max(0,t-.004);snap=math.sin(2*math.pi*(f*3.1)*second)*math.exp(-second/.003)*click if t>=.004 else 0
     value=(body+impact+snap)*attack*(.48 if edge=='up' else 1)
     values.append(value)
    peak=max(abs(x) for x in values);target=(.09 if pid=='silent-red' else .25)*(1.1 if category=='space' else 1)*(.55 if edge=='up' else 1)
    relative=f'{category}/{edge}-{variation+1:02}.wav';path=folder/relative;path.parent.mkdir(exist_ok=True)
    with wave.open(str(path),'wb') as out:
     out.setparams((1,2,48000,0,'NONE','not compressed'));out.writeframes(b''.join(struct.pack('<h',int(x/peak*target*32767)) for x in values))
    names.append(relative)
   samples[category+'/'+edge]=names
 manifest={'Id':pid,'Name':name,'Type':kind,'Color':color,'Author':'MyVoice','Version':'1.0','DefaultVolume':.7,'Samples':samples}
 (folder/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
(root/'LICENSE.txt').write_text('Original procedural sound designs generated for MyVoice. CC0 1.0. No samples from third-party applications. Profile names describe intended character; these are synthesized sounds, not recordings or endorsements of commercial switches.\n',encoding='utf8')
print('Generated',len(profiles),'packs,',len(list(root.rglob('*.wav'))),'original mono 48 kHz samples')
