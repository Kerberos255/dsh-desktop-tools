/** Follow the DSH Session's native permission preset, not a global plugin toggle.
 * Only known low-impact UI operations can omit extra desktop confirmation in
 * danger-full-access. Never use this to approve high-risk named actions.
 */
export function desktopPermissionPreset(ctx,exec){
 const session=exec?.agent?.session;
 if(!session)return null;
 try{return ctx.get?.('permissionPresets')?.current(session)??null;}catch{return null;}
}
const fullExemptReasons=new Set(['point','opaque-type','settings']);
const alwaysConfirmReasons=new Set(['delete','uninstall','send','purchase','transfer','format','shutdown','submit']);
export function desktopApprovalRequired(reason,preset){
 if(!reason)return false;
 if(alwaysConfirmReasons.has(reason))return true;
 return !(preset==='danger-full-access'&&fullExemptReasons.has(reason));
}
