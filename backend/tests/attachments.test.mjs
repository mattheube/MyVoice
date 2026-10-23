import test from 'node:test';
import assert from 'node:assert/strict';
import {decode,previewWave} from '../supabase/functions/community-edit/attachments.ts';
function wav(seconds=1){const size=16000*2*seconds;const bytes=new Uint8Array(44+size),v=new DataView(bytes.buffer);for(const [at,s]of[[0,'RIFF'],[8,'WAVE'],[12,'fmt '],[36,'data']])for(let i=0;i<s.length;i++)bytes[at+i]=s.charCodeAt(i);v.setUint32(4,bytes.length-8,true);v.setUint32(16,16,true);v.setUint16(20,1,true);v.setUint16(22,1,true);v.setUint32(24,16000,true);v.setUint32(28,32000,true);v.setUint16(32,2,true);v.setUint16(34,16,true);v.setUint32(40,size,true);return bytes;}
test('preview accepts bounded PCM and rejects long/truncated/compressed audio',()=>{previewWave(wav());assert.throws(()=>previewWave(wav(31)));assert.throws(()=>previewWave(wav().slice(0,100)));const compressed=wav();new DataView(compressed.buffer).setUint16(20,3,true);assert.throws(()=>previewWave(compressed));});
test('attachment limit applies before and after decoding',()=>{assert.deepEqual([...decode('AQID',3)],[1,2,3]);assert.throws(()=>decode('AQID',2));assert.throws(()=>decode('A'.repeat(1000),3));assert.throws(()=>decode('!',10));});
