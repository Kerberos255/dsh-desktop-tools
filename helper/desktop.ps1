[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RunsRoot)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
try {
 if ($PSVersionTable.PSVersion.Major -ne 7) { throw 'PowerShell 7 required' }
 foreach ($assemblyName in @('UIAutomationClient','UIAutomationTypes','System.Windows.Forms','System.Drawing','WindowsBase')) { Add-Type -AssemblyName $assemblyName }
 $referenceAssemblies = @((Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -File -Filter '*.dll').FullName) + @([Windows.Automation.AutomationElement].Assembly.Location,[Windows.Automation.ControlType].Assembly.Location,[Windows.Rect].Assembly.Location,[Windows.Forms.Form].Assembly.Location,[Windows.Forms.Message].Assembly.Location,[Drawing.Bitmap].Assembly.Location)
 foreach ($desktopDependency in @('System.Private.Windows.GdiPlus.dll','System.Private.Windows.Core.dll')) {
  $desktopDependencyPath = Join-Path $PSHOME $desktopDependency
  if (Test-Path -LiteralPath $desktopDependencyPath -PathType Leaf) { $referenceAssemblies += $desktopDependencyPath }
 }
 Add-Type -Path (Join-Path $PSScriptRoot 'native.cs') -ReferencedAssemblies ($referenceAssemblies | Select-Object -Unique)
 [DshDesktop.Host]::Initialize($RunsRoot,$PSVersionTable.PSVersion.ToString())
 while ($null -ne ($requestLine = [Console]::ReadLine())) {
  $requestId = 0
  try {
   if ($requestLine.Length -gt 1500000) { throw 'Frame limit exceeded' }
   $request = $requestLine | ConvertFrom-Json -AsHashtable
   if ($request.id -isnot [long] -and $request.id -isnot [int]) { throw 'Invalid frame id' }
   $requestId = $request.id
   if ($request.op -isnot [string] -or $request.args -isnot [System.Collections.IDictionary]) { throw 'Invalid frame' }
   $arguments = ConvertTo-Json -InputObject $request.args -Depth 20 -Compress
   $data = [DshDesktop.Host]::Call($request.op,$arguments)
   [Console]::WriteLine((ConvertTo-Json -InputObject @{id=$requestId;ok=$true;data=$data} -Depth 24 -Compress))
  } catch {
   $cause = $_.Exception
   while ($cause.InnerException) { $cause = $cause.InnerException }
   $code = 'action-failed'
   $message = '桌面操作失败，请重新快照核对；已送出的动作可能发生。'
   if ($cause -is [DshDesktop.Failure]) { $code=$cause.Code; $message=$cause.Message }
   [Console]::WriteLine((ConvertTo-Json -InputObject @{id=$requestId;ok=$false;error=@{code=$code;message=$message}} -Depth 8 -Compress))
  }
 }
} catch {
 [Console]::Error.WriteLine('DSH desktop helper initialization failed')
 exit 1
} finally {
 if ('DshDesktop.Host' -as [type]) { try { [DshDesktop.Host]::Call('indicator.hide','{}') | Out-Null } catch {} }
}
