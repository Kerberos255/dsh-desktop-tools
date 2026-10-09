import { renderUntrusted } from './untrusted.js';
import { DesktopFailure } from './engine.js';
export function parseRef(value){if(typeof value!=='string'||!/^\[[a-f0-9]{8}:\d{1,9}\]$/i.test(value))throw new DesktopFailure('element-gone','引用格式无效，请重新 desktop_snapshot');return value;}
export const identity=window=>[window.handle,window.pid,window.startedAt,window.process,window.className].join('|');
export function shellOnly(window,nodes){return /Chrome_WidgetWin|Chromium|Electron/i.test(window.className??'')&&nodes.filter(row=>!['Window','Pane','TitleBar','Border','Button'].includes(row.role)||row.role==='Button'&&!/关闭|最小化|最大化|还原|close|minimi|maximi|restore/i.test(row.name)).length===0;}
export function buildSnapshot(raw,state,maxChars,extras={}){
 state.refs.clear();state.screenshot=null;const nodes=[],meta={handle:raw.handle,process:raw.process,title:raw.titleHidden?'[保密输入窗口]':String(raw.title??'').slice(0,512),generation:raw.generation,electronShell:shellOnly(raw,raw.nodes??[])};let truncated=!!raw.truncated;
 if(meta.electronShell)meta.hint='本次快照只有窗口外框；可连接的网页或 Electron 页面可使用 browser_*，其他界面可经审批使用截图坐标。';
 for(const item of raw.nodes??[]){const {runtimeId,windowIdentity,...row}=item;row.name=String(row.name??'').slice(0,512);if(renderUntrusted({...meta,nodes:[...nodes,row],truncated:true,...extras})[0].text.length>maxChars){truncated=true;continue;}nodes.push(row);state.refs.set(row.ref,{...item,windowIdentity:identity(raw)});}
 return {...meta,nodes,truncated};
}
