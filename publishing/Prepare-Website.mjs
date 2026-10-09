import fs from 'node:fs/promises';
const repository='mattheube/MyVoice';
const headers={'Accept':'application/vnd.github+json','User-Agent':'MyVoice-Website'};
if(process.env.GITHUB_TOKEN)headers.Authorization='Bearer '+process.env.GITHUB_TOKEN;
async function json(url,auth=false){const response=await fetch(url,{headers:auth?headers:undefined,signal:AbortSignal.timeout(20000)});if(!response.ok)throw Error(`HTTP ${response.status}: ${url}`);const text=await response.text();if(text.length>2000000)throw Error('Unexpected response size');return JSON.parse(text.replace(/^\uFEFF/,''));}
const releases=await json(`https://api.github.com/repos/${repository}/releases?per_page=100`,true);
const candidates=releases.filter(r=>!r.draft&&r.prerelease&&/^v\d+\.\d+\.\d+-beta\.[1-9]\d*$/.test(r.tag_name));
const parts=tag=>tag.match(/\d+/g).map(Number);
candidates.sort((a,b)=>{const x=parts(a.tag_name),y=parts(b.tag_name);for(let i=0;i<4;i++){if(x[i]!==y[i])return y[i]-x[i]}return 0});
if(candidates.length){
 const release=candidates[0],tag=release.tag_name;
 const manifest=await json(`https://github.com/${repository}/releases/download/${tag}/manifest.json`);
 const version=tag.slice(1).split('-')[0];
 if(manifest.tag!==tag||manifest.version!==version||manifest.channel!=='beta'||!/^[a-f0-9]{64}$/i.test(manifest.sha256)||manifest.downloadUrl!==`https://github.com/${repository}/releases/download/${tag}/MyVoiceSetup.exe`||manifest.releaseUrl!==`https://github.com/${repository}/releases/tag/${tag}`)throw Error('Invalid official Beta manifest');
 await fs.mkdir('website/updates',{recursive:true});await fs.writeFile('website/updates/beta.json',JSON.stringify(manifest,null,2)+'\n');
 console.log('Prepared official Beta channel: '+tag);
}else{console.log('No published Beta yet; website has no Beta update pointer.');}
