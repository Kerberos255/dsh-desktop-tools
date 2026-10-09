import { randomUUID } from 'node:crypto';
import { DesktopFailure } from './engine.js';
import { tierFor,confirmationFor } from './policy.js';
import { desktopApprovalRequired } from './permissions.js';
import { identity,buildSnapshot } from './snapshot.js';
export const safeTitle=window=>window.titleHidden?'[保密输入窗口]':window.title;
export const publicWindow=window=>({...Object.fromEntries(['handle','pid','process','className','foreground','minimized','rect'].map(key=>[key,window[key]])),title:safeTitle(window)});
const sameRect=(a,b)=>a&&b&&['x','y','w','h'].every(key=>a[key]===b[key]);
const approvalLabels={point:'按截图坐标点击，无法通过控件语义核对具体动作','opaque-type':'向当前焦点输入，应用没有可核对的输入控件',submit:'提交输入（Enter）'};
export class Actions{
 constructor(engine,artifacts,{approve,credential,readOnly,permissionPreset}={}){this.engine=engine;this.artifacts=artifacts;this.approve=approve;this.credential=credential;this.readOnly=readOnly;this.permissionPreset=permissionPreset;}
 allowed(window,config,mutation=false,exec){const tier=tierFor(window,config,this.permissionPreset?.(exec));if(tier==='blocked'||mutation&&(tier!=='interact'||!config.allowMutations||this.readOnly?.(exec)))throw new DesktopFailure('tier-denied',mutation?'此窗口或当前会话禁止桌面写操作；可在桌面插件设置页核对写操作开关与进程权限。':'此进程已禁止读取。',{tier});return tier;}
 async window(args,state,config,exec,mutation=false){const window=await this.engine.request('window.info',{selector:args.handle},exec.signal);this.allowed(window,config,mutation,exec);state.process=window.process;if(state.window&&identity(state.window)!==identity(window)){state.refs.clear();state.screenshot=null;}state.window=window;return window;}
 async target(window,args,state,exec){const cached=state.refs.get(args.ref);if(!cached||cached.windowIdentity!==identity(window))throw new DesktopFailure('element-gone','引用已失效或属于其他窗口，请重新 desktop_snapshot');const fresh=await this.engine.request('element.info',{owner:state.owner,handle:window.handle,ref:args.ref},exec.signal);if(['role','name','automationId','windowIdentity'].some(key=>fresh[key]!==cached[key]))throw new DesktopFailure('element-gone','控件已变化，请重新 desktop_snapshot');return fresh;}
 screenshot(window,args,state){const shot=state.screenshot;if(!shot||shot.id!==args.screenshotRef||shot.identity!==identity(window)||shot.title!==window.title||!sameRect(shot.windowRect,window.rect)||Date.now()-shot.created>300000)throw new DesktopFailure('element-gone','截图已过期、属于其他会话或窗口位置已变化，请重新 desktop_screenshot');return shot;}
 async confirm(reason,window,target,args,state,config,exec){if(!desktopApprovalRequired(reason,this.permissionPreset?.(exec)))return;const outcome=await this.approve?.({agent:exec.agent,toolName:exec.name,callId:exec.callId,reason:'桌面操作：'+(approvalLabels[reason]??reason)+' · '+window.process+' · '+String(target?.name??safeTitle(window)).slice(0,160),signal:exec.signal});exec.signal.throwIfAborted();if(outcome!=='allowed-once')throw new DesktopFailure('needs-confirmation','原生审批未授权此桌面动作：'+(outcome??'unavailable'));const fresh=await this.engine.request('window.info',{selector:window.handle},exec.signal);this.allowed(fresh,config,true,exec);if(identity(fresh)!==identity(window)||fresh.title!==window.title)throw new DesktopFailure('element-gone','审批期间目标窗口已变化，请重新快照');if(target)await this.target(fresh,args,state,exec);else this.screenshot(fresh,args,state);}
 async physical(op,payload,window,state,config,exec){if(!config.allowPhysicalInput)throw new DesktopFailure('unsupported-action','此控件需要真实键鼠兜底；可在桌面插件设置页开启');state.mutating=true;state.physical=true;return this.engine.request(op,{...payload,owner:state.owner,handle:window.handle,expectedWindow:identity(window),requireIndicator:config.requireIndicator,holdMs:600},exec.signal);}
 async run(name,args,exec,state,config){
  if(name==='desktop_windows'){if(config.requireIndicator)await this.engine.request('indicator.show',{},exec.signal);try{const result=await this.engine.request('windows.list',{process:args.process,onlyVisible:args.onlyVisible!==false},exec.signal);const windows=(result.windows??[]).map(window=>({...publicWindow(window),tier:tierFor(window,config,this.permissionPreset?.(exec)),...tierFor(window,config,this.permissionPreset?.(exec))==='blocked'?{title:'已屏蔽'}:{}}));let rows=[],truncated=!!result.truncated;for(const window of windows){if(JSON.stringify([...rows,window]).length>config.maxSnapshotChars){truncated=true;continue;}rows.push(window);}return {windows:rows,truncated};}finally{if(config.requireIndicator&&this.engine.hello)await this.engine.request('indicator.hide',{},new AbortController().signal).catch(()=>{});}}
  const mutation=['desktop_click','desktop_type'].includes(name),window=await this.window(args,state,config,exec,mutation);
  // The user must see DSH working even for semantic UIA, snapshots and screenshots.
  // Physical fallback may replace this window marker with a precise target ring.
  if(config.requireIndicator)await this.engine.request('indicator.show',{handle:window.handle},exec.signal);
  try{
  if(name==='desktop_snapshot'){const result=await this.engine.request('window.snapshot',{handle:window.handle,expectedWindow:identity(window),owner:state.owner,prefix:state.prefix,depth:args.depth??12,interactiveOnly:!!args.interactiveOnly,onlyNamed:args.onlyNamed!==false,maxNodes:Math.min(800,Math.ceil((args.maxChars??config.maxSnapshotChars)/35)),maxMillis:1500},exec.signal);const extras={untrustedContent:true,runDirectory:await this.artifacts.directory(state)},value=buildSnapshot(result,state,Math.min(args.maxChars??config.maxSnapshotChars,config.maxSnapshotChars),extras);await this.artifacts.snapshot(state,value);return value;}
  if(name==='desktop_screenshot'){const maxBytes=Math.min(args.maxBytes??config.maxScreenshotBytes,config.maxScreenshotBytes),file=await this.artifacts.pngPath(state,maxBytes);const result=await this.engine.request('window.screenshot',{handle:window.handle,expectedWindow:identity(window),path:file,rect:args.rect,maxBytes,maxWidth:config.maxScreenshotWidth,maxHeight:config.maxScreenshotHeight},exec.signal);const png=await this.artifacts.png(file,maxBytes);const id='shot-'+randomUUID();state.screenshot={id,identity:identity(window),title:window.title,windowRect:window.rect,captureRect:result.captureRect,width:png.width,height:png.height,created:Date.now()};return {path:file,...png,redacted:!!result.redacted,screenshotRef:id,handle:window.handle,captureRect:result.captureRect,scale:{x:png.width/result.captureRect.w,y:png.height/result.captureRect.h}};}
  const target=args.ref?await this.target(window,args,state,exec):null;
  if(name==='desktop_click'){
   let method=args.method??'auto';if(method==='auto')method=['invoke','select','toggle','expand'].find(key=>target?.patterns.includes(key))??'point';
   if(method!=='point'&&!target?.patterns.includes(method))throw new DesktopFailure('unsupported-action','该控件不支持所选语义动作');
   if(method==='point'&&!config.allowPhysicalInput)throw new DesktopFailure('unsupported-action','此动作需要真实键鼠兜底；可在桌面插件设置页开启');
   const shot=!target?this.screenshot(window,args,state):null;
   if(shot&&(args.x<0||args.y<0||args.x>=shot.width||args.y>=shot.height))throw new DesktopFailure('input-denied','坐标超出截图范围');
   await this.confirm(confirmationFor(!target?'point':'click',{...target,title:safeTitle(window)},config),window,target,args,state,config,exec);
   if(method==='point'){
    let point;if(target){const fresh=await this.target(window,args,state,exec);point={x:Math.round(fresh.rect.x+fresh.rect.w/2),y:Math.round(fresh.rect.y+fresh.rect.h/2)};}else{if(args.x<0||args.y<0||args.x>=shot.width||args.y>=shot.height)throw new DesktopFailure('input-denied','坐标超出截图范围');point={x:Math.round(shot.captureRect.x+args.x*shot.captureRect.w/shot.width),y:Math.round(shot.captureRect.y+args.y*shot.captureRect.h/shot.height)};}
    return this.physical('input.point',{point,...target?{ref:args.ref}:{}},window,state,config,exec);
   }
   state.mutating=true;return this.engine.request('element.invoke',{owner:state.owner,handle:window.handle,expectedWindow:identity(window),ref:args.ref,method},exec.signal);
  }
  if(name==='desktop_type'){
   if(args.submit&&!config.allowPhysicalInput)throw new DesktopFailure('unsupported-action','提交输入需要真实 Enter 键；请先开启真实键鼠兜底');
   if(target?.password&&!args.secret)throw new DesktopFailure('input-denied','密码框使用 secret + 文件或已绑定进程的凭据引用');
   if(!target)this.screenshot(window,args,state);
   await this.confirm(confirmationFor(args.submit?'submit':target?'type':'opaque-type',{...target,title:safeTitle(window)},config),window,target,args,state,config,exec);
   let text;if(args.credentialRef){const allowed=JSON.parse(config.credentialRulesJson).some(row=>row.ref===args.credentialRef&&tierFor(window,{defaultTier:'blocked',processesJson:JSON.stringify([{match:row.match,tier:'interact'}])})==='interact');if(!allowed)throw new DesktopFailure('input-denied','此凭据未绑定当前进程');text=await this.credential?.(args.credentialRef,exec.signal);}else text=args.fromFile?await this.artifacts.input(state,args.fromFile):args.text;
   if(typeof text!=='string'||text.length>65536)throw new DesktopFailure('input-denied','输入内容无效或超过 65536 字符');
   let result;const semantic=target?.patterns.includes('value');
   if(semantic){state.mutating=true;result=await this.engine.request('element.setValue',{owner:state.owner,handle:window.handle,expectedWindow:identity(window),ref:args.ref,text,secret:!!args.secret,clear:args.clear!==false},exec.signal);}
   else result=await this.physical('input.keys',{...target?{ref:args.ref}:{},text,secret:!!args.secret,clear:args.clear!==false,submit:!!args.submit},window,state,config,exec);
   if(semantic&&args.submit)await this.physical('input.keys',{ref:args.ref,submit:true,clear:false,secret:!!args.secret},window,state,config,exec);
   if(config.recordText&&!args.secret)await this.artifacts.log(state,{tool:name,phase:'text',text});text=undefined;return {...result,submitted:!!args.submit,...args.secret?{verification:'hidden'}:{}};
  }
  throw new DesktopFailure('unsupported-action','未知桌面动作');
  }finally{if(config.requireIndicator&&this.engine.hello)await this.engine.request('indicator.hide',{},new AbortController().signal).catch(()=>{});}
 }
}
