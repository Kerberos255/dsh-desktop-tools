import fs from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { DesktopFailure } from './engine.js';
import { pruneScreenshots } from './retention.js';
export class Artifacts{
 constructor(root,config){this.root=path.resolve(root);this.config=config;}
 async cleanupScreenshots(activeRuns=new Set()){return pruneScreenshots(this.root,{retentionDays:this.config().screenshotRetentionDays,activeRuns});}
 async directory(state){const directory=path.join(this.root,state.run);await fs.mkdir(directory,{recursive:true});if(await fs.realpath(directory)!==directory||await fs.realpath(this.root)!==this.root)throw new DesktopFailure('artifact-denied','产物目录不允许目录联接');return directory;}
 async file(filename,allowMissing=false){try{const info=await fs.lstat(filename);if(!info.isFile()||info.isSymbolicLink()||info.nlink>1)throw new DesktopFailure('artifact-denied','产物不允许链接或特殊文件');return info;}catch(error){if(allowMissing&&error.code==='ENOENT')return null;throw error;}}
 async quota(state,extra=0){const directory=await this.directory(state);let bytes=extra;for(const row of await fs.readdir(directory,{withFileTypes:true})){if(!row.isFile()||row.isSymbolicLink())throw new DesktopFailure('artifact-denied','本会话产物目录包含子目录或链接');bytes+=(await this.file(path.join(directory,row.name))).size;}if(bytes>this.config().maxRunBytes)throw new DesktopFailure('artifact-limit','本会话桌面产物已达到上限');return directory;}
 async log(state,row){const directory=await this.quota(state,4096),file=path.join(directory,'actions.jsonl');const info=await this.file(file,true);if(info?.size>4*1024*1024)throw new DesktopFailure('artifact-limit','本会话桌面审计已达到上限');await fs.appendFile(file,JSON.stringify({ts:new Date().toISOString(),...row})+'\n',{encoding:'utf8'});}
 async snapshot(state,value){const directory=await this.quota(state,65536),file=path.join(directory,'snapshot.json');await this.file(file,true);await fs.writeFile(file,JSON.stringify(value,null,2)+'\n','utf8');}
 async pngPath(state,maxBytes){const directory=await this.quota(state,maxBytes);return path.join(directory,'screenshot-'+Date.now()+'-'+randomUUID().slice(0,8)+'.png');}
 async png(filename,maxBytes){const info=await this.file(filename);if(info.size>maxBytes)throw new DesktopFailure('artifact-limit','截图超过大小限制');const buffer=await fs.readFile(filename);if(buffer.length<24||buffer.readUInt32BE(0)!==0x89504e47||buffer.toString('ascii',1,4)!=='PNG')throw new DesktopFailure('artifact-denied','截图不是有效 PNG');return {width:buffer.readUInt32BE(16),height:buffer.readUInt32BE(20),bytes:info.size};}
 async input(state,relative){const directory=await this.directory(state);if(typeof relative!=='string'||!relative||path.basename(relative)!==relative||path.isAbsolute(relative)||/[:\x00-\x1f]/.test(relative)||/[. ]$/.test(relative))throw new DesktopFailure('input-denied','fromFile 仅接受本会话产物目录内的单个文件名');const file=path.resolve(directory,relative);if(await fs.realpath(file)!==file||!file.startsWith(directory+path.sep))throw new DesktopFailure('input-denied','输入文件不能越出本会话产物目录');const info=await this.file(file);if(info.size>1048576)throw new DesktopFailure('input-denied','输入文件超过 1 MiB');return fs.readFile(file,'utf8');}
}
