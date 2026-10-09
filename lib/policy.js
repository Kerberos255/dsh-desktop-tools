import fs from 'node:fs';
export const defaults=JSON.parse(fs.readFileSync(new URL('../policy.json',import.meta.url),'utf8'));
const tiers=new Set(['interact','read','blocked']);
const protectedProcesses=new Set(['consent.exe','logonui.exe','winlogon.exe','lockapp.exe','credentialuibroker.exe']);
export function processRules(text){
 const rows=JSON.parse(text);if(!Array.isArray(rows)||rows.length>100)throw new Error('进程规则需要至多 100 项的数组');
 const seen=new Set();for(const row of rows){if(!row||Object.keys(row).some(key=>!['match','tier','imagePath'].includes(key))||typeof row.match!=='string'||! /^[\w .*-]{1,128}\.exe$/i.test(row.match)||!tiers.has(row.tier)||row.imagePath!==undefined&&(typeof row.imagePath!=='string'||!/^(?:[A-Za-z]:[\\/]|\\\\)/.test(row.imagePath)))throw new Error('进程规则无效');const key=row.match.toLowerCase();if(seen.has(key))throw new Error('进程规则重复');seen.add(key);}return rows;
}
export function tierFor(window,config,preset=null){
 if(window.secure||protectedProcesses.has(String(window.process).toLowerCase())||/credential|cred.?dialog/i.test(window.className??''))return 'blocked';
 let winner=null;for(const row of processRules(config.processesJson)){
  const pattern='^'+row.match.replace(/[.+?^${}()|[\]\\]/g,'\\$&').replaceAll('*','.*')+'$';
  if(!new RegExp(pattern,'i').test(window.process)||row.imagePath&&row.imagePath.toLowerCase()!==String(window.imagePath).toLowerCase())continue;
  const score=row.match.replaceAll('*','').length+(row.imagePath?10000:0);if(!winner||score>winner.score||score===winner.score&&({interact:0,read:1,blocked:2}[row.tier]>{interact:0,read:1,blocked:2}[winner.tier]))winner={score,tier:row.tier};
 }return winner?.tier??(preset==='danger-full-access'&&config.defaultTier==='read'?'interact':config.defaultTier);
}
const words={delete:/删除|移除|清空|delete|remove|erase/i,uninstall:/卸载|uninstall/i,send:/发送|提交|发布|回复|send|submit|publish|reply/i,purchase:/购买|付款|支付|purchase|buy|pay/i,transfer:/转账|汇款|transfer|remit/i,format:/格式化|format/i,shutdown:/关机|重启|shutdown|restart/i,settings:/设置|配置|settings|preferences|configure/i};
export function confirmationFor(kind,target,config){
 // Return canonical reason IDs: approval policy must not parse translated UI text.
 if(kind==='point')return 'point';
 if(kind==='opaque-type')return 'opaque-type';
 if(kind==='submit')return 'submit';
 const text=[target.name,target.title].filter(Boolean).join(' ');return config.confirmations.find(key=>words[key]?.test(text))??null;
}
