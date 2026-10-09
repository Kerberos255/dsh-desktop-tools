import fs from 'node:fs/promises';
import path from 'node:path';

const runName=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const screenshotName=/^screenshot-[0-9]{10,16}-[0-9a-f]{8}\.png$/i;
const dayMs=24*60*60*1000;

/**
 * Narrow, best-effort retention sweep: only screenshot PNGs that this plugin
 * named, directly under its private UUID run directories. Other artifacts
 * and non-regular/linked entries are never removed.
 */
export async function pruneScreenshots(root,{retentionDays=7,activeRuns=new Set(),now=Date.now()}={}){
 const report={scanned:0,deleted:0,skipped:0};
 if(!Number.isSafeInteger(retentionDays)||retentionDays<0||retentionDays>365)
  throw new RangeError('screenshot retentionDays must be in [0,365]');
 if(retentionDays===0)return report;
 const resolved=path.resolve(root);
 let info;
 try{info=await fs.lstat(resolved);}
 catch(error){if(error.code==='ENOENT')return report;throw error;}
 if(!info.isDirectory()||info.isSymbolicLink()||await fs.realpath(resolved)!==resolved)
  throw new Error('desktop screenshot cleanup root is not a real directory');
 const expiry=now-retentionDays*dayMs;
 for(const folder of await fs.readdir(resolved,{withFileTypes:true})){
  if(!runName.test(folder.name)||!folder.isDirectory()||folder.isSymbolicLink()||activeRuns.has(folder.name)){report.skipped++;continue;}
  const subdir=path.join(resolved,folder.name);
  // Never traverse a link/junction, including Windows reparse-point directories.
  let real;try{real=await fs.realpath(subdir);}catch{report.skipped++;continue;}
  if(real!==subdir){report.skipped++;continue;}
  let files;
  try{files=await fs.readdir(subdir,{withFileTypes:true});}catch{report.skipped++;continue;}
  for(const file of files){
   if(!screenshotName.test(file.name)||!file.isFile()||file.isSymbolicLink())continue;
   const location=path.join(subdir,file.name);
   let stat;
   try{stat=await fs.lstat(location);}catch{report.skipped++;continue;}
   if(!stat.isFile()||stat.isSymbolicLink()||stat.nlink!==1||!Number.isFinite(stat.mtimeMs))continue;
   report.scanned++;
   if(stat.mtimeMs>=expiry)continue;
   try{
    // Check the parent again immediately before unlink; never remove audit/input.
    if(await fs.realpath(subdir)!==subdir){report.skipped++;continue;}
    await fs.unlink(location);
    report.deleted++;
   }catch{report.skipped++;}
  }
 }
 return report;
}
