// PKCE secrets are scoped to this tab and removed after one callback or cancellation.
export const verifierKey='myvoice.discord.pkce';
const sessionKey='myvoice.account.session';
const encode=bytes=>btoa(String.fromCharCode(...bytes)).replaceAll('+','-').replaceAll('/','_').replace(/=+$/,'');
export async function createPkce(){const verifier=encode(crypto.getRandomValues(new Uint8Array(48)));return {verifier,challenge:encode(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(verifier))))};}
export function captureAuthCallback(location,history){
 const query=new URLSearchParams(location.search),fragment=new URLSearchParams(location.hash.slice(1));
 const result={code:query.get('code'),error:query.get('error'),legacy:fragment.has('access_token')?Object.fromEntries(fragment):null};
 if(result.code||result.error||result.legacy)history.replaceState(null,'',location.pathname+'#account');
 return result;
}
export function saveSession(storage,value){if(value?.access_token&&value?.refresh_token)storage.setItem(sessionKey,JSON.stringify({...value,expires_at:value.expires_at||Math.floor(Date.now()/1000)+(value.expires_in||3600)}));else storage.removeItem(sessionKey);return value;}
export function readSession(storage){try{const value=JSON.parse(storage.getItem(sessionKey)||'null');return typeof value?.access_token==='string'&&typeof value?.refresh_token==='string'?value:null;}catch{storage.removeItem(sessionKey);return null;}}
export function username(value){const name=String(value).trim().replace(/^@/,'').toLowerCase();if(!/^[a-z0-9_]{3,24}$/.test(name)||['myvoice','admin','administrator','support','moderator','official','system'].includes(name))throw Error('Identifiant : 3 à 24 lettres, chiffres ou _, sans espace.');return name;}
export function passwordScore(value){if(value.length<10||new Set(value).size<5||/password|motdepasse|1234567890|qwerty|azerty/i.test(value))return 0;return value.length>=20?3:value.length>=14?2:1;}
export async function refreshSession(config,storage,request=fetch){
 const current=readSession(storage);if(!current||Number(current.expires_at)>Date.now()/1000+120)return current;
 const response=await request(config.url+'/auth/v1/token?grant_type=refresh_token',{method:'POST',headers:{apikey:config.publishableKey,'Content-Type':'application/json'},body:JSON.stringify({refresh_token:current.refresh_token}),signal:AbortSignal.timeout(10000)});
 if(!response.ok){if(response.status===400||response.status===401){saveSession(storage,null);return null;}throw Error('Connexion momentanément indisponible. Réessayez.');}
 const value=await response.json();if(!value.access_token||!value.refresh_token)throw Error('Session incomplète. Réessayez.');return saveSession(storage,value);
}
export async function startDiscord(config,storage,request=fetch){
 const settings=await request(config.url+'/auth/v1/settings',{headers:{apikey:config.publishableKey},signal:AbortSignal.timeout(10000)});
 if(!settings.ok||!(await settings.json()).external?.discord)throw Error('La connexion Discord n’est pas encore activée sur le serveur MyVoice.');
 const pkce=await createPkce();storage.setItem(verifierKey,JSON.stringify({verifier:pkce.verifier,created:Date.now()}));
 const query=new URLSearchParams({provider:'discord',redirect_to:'https://mattheube.github.io/MyVoice/',code_challenge:pkce.challenge,code_challenge_method:'s256'});
 return config.url+'/auth/v1/authorize?'+query;
}
export async function finishDiscord(config,callback,storage,request=fetch){
 const raw=storage.getItem(verifierKey);storage.removeItem(verifierKey);
 if(callback.error)throw Error('Connexion Discord annulée. Vous pouvez réessayer.');
 let transaction;try{transaction=JSON.parse(raw||'null');}catch{}
 if(!transaction||typeof transaction.verifier!=='string'||Date.now()-transaction.created>300000||Date.now()<transaction.created)throw Error('Connexion expirée. Relancez « Continuer avec Discord » dans ce navigateur.');
 if(typeof callback.code!=='string'||!callback.code||callback.code.length>2048)throw Error('Code de connexion invalide.');
 const response=await request(config.url+'/auth/v1/token?grant_type=pkce',{method:'POST',headers:{apikey:config.publishableKey,'Content-Type':'application/json'},body:JSON.stringify({auth_code:callback.code,code_verifier:transaction.verifier}),signal:AbortSignal.timeout(10000)});
 if(!response.ok)throw Error('La connexion n’a pas abouti. Relancez « Continuer avec Discord ».');
 const session=await response.json();if(!session.access_token||!session.refresh_token)throw Error('Session incomplète. Réessayez.');return saveSession(storage,session);
}
