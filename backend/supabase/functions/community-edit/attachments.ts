export const decode=(value:unknown,maximum:number)=>{if(typeof value!=='string'||value.length>maximum*4/3+8)throw Error('Invalid attachment');const bytes=Uint8Array.from(atob(value),c=>c.charCodeAt(0));if(bytes.length>maximum)throw Error('Attachment too large');return bytes;};
export function previewWave(bytes:Uint8Array){
 const ascii=(at:number,n:number)=>String.fromCharCode(...bytes.slice(at,at+n));
 if(bytes.length<44||ascii(0,4)!=='RIFF'||ascii(8,4)!=='WAVE')throw Error('WAV required');
 const view=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);let rate=0,channels=0,bits=0,dataSize=0;
 for(let at=12;at+8<=bytes.length;){const size=view.getUint32(at+4,true);if(at+8+size>bytes.length)throw Error('Invalid WAV');if(ascii(at,4)==='fmt '){if(size<16||view.getUint16(at+8,true)!==1)throw Error('PCM required');channels=view.getUint16(at+10,true);rate=view.getUint32(at+12,true);bits=view.getUint16(at+22,true);}if(ascii(at,4)==='data')dataSize+=size;at+=8+size+(size%2);}
 if(![1,2].includes(channels)||bits!==16||rate<16000||rate>48000||dataSize<=0||dataSize/(rate*channels*2)>30)throw Error('Preview must be PCM16, 16–48 kHz and at most 30 seconds');
}
