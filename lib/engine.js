import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
export class DesktopFailure extends Error{constructor(code,message,details={}){super(message);this.name='DesktopFailure';this.code=code;this.details=details;}}
export class Engine{
 constructor(config,root,{launch=spawn,onReset=()=>{}}={}){this.config=config;this.root=root;this.launch=launch;this.onReset=onReset;this.pending=new Map();this.nextId=0;this.generation=0;this.child=null;this.starting=null;this.closed=false;this.notice=false;this.hello=null;}
 async ready(signal){
  signal.throwIfAborted();if(this.closed)throw new DesktopFailure('engine-unavailable','桌面插件已卸载');
  if(this.notice){this.notice=false;throw new DesktopFailure('engine-unavailable','桌面引擎已退出；本次未重放动作，下次调用重新启动');}
  if(this.child&&this.hello)return;
  if(!this.starting){const task=this.start(signal);this.starting=task;task.finally(()=>{if(this.starting===task)this.starting=null;}).catch(()=>{});}
  await this.starting;signal.throwIfAborted();
 }
 async start(signal){
  if(process.platform!=='win32')throw new DesktopFailure('engine-unavailable','桌面工具目前支持 Windows');
  const config=this.config(),generation=++this.generation,child=this.launch(config.powershellPath,['-STA','-NoProfile','-NoLogo','-NonInteractive','-ExecutionPolicy','Bypass','-File',fileURLToPath(new URL('../helper/desktop.ps1',import.meta.url)),'-RunsRoot',this.root],{windowsHide:true,stdio:['pipe','pipe','pipe'],env:{...process.env,TEMP:process.env.TEMP??this.root,TMP:process.env.TMP??this.root}});this.child=child;
  let buffer='';child.stdout.setEncoding('utf8');child.stdout.on('data',chunk=>{if(generation!==this.generation)return;buffer+=chunk;if(buffer.length>2*1024*1024){this.fault(new DesktopFailure('engine-unavailable','桌面引擎返回数据超限'));return;}let split;while((split=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,split).trim();buffer=buffer.slice(split+1);if(!line)continue;let frame;try{frame=JSON.parse(line);}catch{this.fault(new DesktopFailure('engine-unavailable','桌面引擎协议格式无效'));return;}if(!Number.isSafeInteger(frame.id)||frame.id<=0||typeof frame.ok!=='boolean'||frame.ok&&frame.data===undefined||!frame.ok&&(typeof frame.error?.code!=='string'||typeof frame.error?.message!=='string')){this.fault(new DesktopFailure('engine-unavailable','桌面引擎协议字段无效'));return;}const pending=this.pending.get(frame.id);if(!pending)continue;this.pending.delete(frame.id);pending.finish();if(frame.ok===true&&frame.data!==undefined)pending.resolve(frame.data);else pending.reject(new DesktopFailure(frame.error?.code??'engine-unavailable',frame.error?.message??'桌面引擎操作失败'));}});
  // Diagnostics may contain application-supplied strings. Keep stderr off tool output.
  child.stderr.on('data',()=>{});child.on('error',()=>{if(this.child===child)this.fault(new DesktopFailure('engine-unavailable','无法启动 PowerShell 7 桌面引擎，请检查安装和插件设置'));});
  child.stdin.on?.('error',()=>{if(this.child===child)this.fault(new DesktopFailure('engine-unavailable','桌面引擎输入连接已断开'));});
  child.on('exit',()=>{if(this.child!==child)return;const waiting=this.pending.size>0;this.notice=!waiting;this.stop(new DesktopFailure('engine-unavailable','桌面引擎已退出，动作未自动重放'));});
  try{const hello=await this.send('hello',{},AbortSignal.any([signal,AbortSignal.timeout(15000)]));if(this.child!==child||generation!==this.generation||hello.protocol!==1||!hello.dpiAware||!hello.powershell?.startsWith('7.'))throw new DesktopFailure('engine-unavailable','桌面引擎握手失败：协议、DPI 或 PowerShell 版本不兼容');this.hello=hello;}catch(error){this.stop(error);throw error;}
 }
 send(op,args,signal){
  signal.throwIfAborted();const child=this.child;if(!child)throw new DesktopFailure('engine-unavailable','桌面引擎未连接');
  const id=++this.nextId,frame=JSON.stringify({id,op,args});if(frame.length>1500000)throw new DesktopFailure('input-denied','桌面请求过大');
  return new Promise((resolve,reject)=>{const abort=()=>{const reason=new DesktopFailure('timeout','桌面操作已取消或超时；已送出的动作可能发生，请重新快照核对');this.stop(reason);};const finish=()=>signal.removeEventListener('abort',abort);this.pending.set(id,{resolve,reject,finish});signal.addEventListener('abort',abort,{once:true});if(signal.aborted){abort();return;}child.stdin.write(frame+'\n',error=>{if(error)this.stop(new DesktopFailure('engine-unavailable','桌面引擎连接已断开'));});});
 }
 async request(op,args={},signal=new AbortController().signal){await this.ready(signal);return this.send(op,args,signal);}
 stop(reason=new DesktopFailure('engine-unavailable','桌面引擎连接已重置')){const child=this.child;this.child=null;this.hello=null;this.generation++;for(const pending of this.pending.values()){pending.finish();pending.reject(reason);}this.pending.clear();this.onReset();if(child&&!child.killed)child.kill();}
 fault(reason){this.notice=this.pending.size===0;this.stop(reason);}
 reset(){this.notice=false;this.stop();}
 close(){this.closed=true;this.reset();}
}
