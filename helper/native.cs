using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace DshDesktop {
public sealed class Failure : Exception {
 public string Code { get; private set; }
 public Failure(string code,string message) : base(message) { Code=code; }
}
public sealed class Box {
 public int x {get;set;} public int y {get;set;} public int w {get;set;} public int h {get;set;}
 public Rectangle GetRectangle(){return new Rectangle(x,y,w,h);}
 public static Box From(Rectangle r){return new Box{x=r.X,y=r.Y,w=r.Width,h=r.Height};}
}
public sealed class WindowData {
 public string handle {get;set;} public int pid {get;set;} public string process {get;set;} public string imagePath {get;set;}
 public string className {get;set;} public string title {get;set;} public string startedAt {get;set;}
 public bool titleHidden {get;set;} public bool foreground {get;set;} public bool minimized {get;set;} public bool secure {get;set;} public bool elevated {get;set;}
 public Box rect {get;set;}
 public string Key {get{return String.Join("|",handle,pid,startedAt,process,className);}}
}
public sealed class NodeData {
 public string @ref {get;set;} public string runtimeId {get;set;} public string windowIdentity {get;set;}
 public string role {get;set;} public string name {get;set;} public string automationId {get;set;} public string className {get;set;}
 public bool enabled {get;set;} public bool offscreen {get;set;} public bool password {get;set;} public bool valueHidden {get;set;}
 public Box rect {get;set;} public string[] patterns {get;set;}
}
internal sealed class Entry {
 public AutomationElement Element; public string Runtime; public string Role; public string Name; public string AutomationId; public string Owner; public WindowData Window;
}
internal static class Win {
 [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;public POINT(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
 public delegate bool EnumProc(IntPtr hwnd,IntPtr lparam);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd,StringBuilder value,int length);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd,StringBuilder value,int length);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd,int command);
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd,uint flag);
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
 [DllImport("user32.dll",SetLastError=true)] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] public static extern IntPtr GetThreadDpiAwarenessContext();
 [DllImport("user32.dll")] public static extern bool AreDpiAwarenessContextsEqual(IntPtr a,IntPtr b);
 [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT point,uint flags);
 [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr handle);
 [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int index,uint dpi);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
 [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
 [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder value,int length,out int needed);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
 [DllImport("advapi32.dll")] public static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
 [DllImport("advapi32.dll")] public static extern bool GetTokenInformation(IntPtr token,int kind,out int value,int size,out int needed);
 [StructLayout(LayoutKind.Sequential)] public struct LASTINPUT {public uint size,time;}
 [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUT info);
 [StructLayout(LayoutKind.Sequential)] public struct MOUSE {public int x,y;public uint data,flags,time;public IntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] public struct KEYBOARD {public ushort vk,scan;public uint flags,time;public IntPtr extra;}
 [StructLayout(LayoutKind.Explicit)] public struct UNION {[FieldOffset(0)]public MOUSE mouse;[FieldOffset(0)]public KEYBOARD key;}
 [StructLayout(LayoutKind.Sequential)] public struct INPUT {public uint type;public UNION value;}
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint count,INPUT[] values,int size);
 public static uint LastInput(){var value=new LASTINPUT{size=(uint)Marshal.SizeOf<LASTINPUT>()};if(!GetLastInputInfo(ref value))throw new Failure("input-denied","无法检测当前键鼠状态");return value.time;}
 public static bool Elevated(int pid){IntPtr process=OpenProcess(0x1000,false,(uint)pid),token=IntPtr.Zero;try{int elevated,needed;if(process==IntPtr.Zero||!OpenProcessToken(process,8,out token)||!GetTokenInformation(token,20,out elevated,4,out needed))return true;return elevated!=0;}finally{if(token!=IntPtr.Zero)CloseHandle(token);if(process!=IntPtr.Zero)CloseHandle(process);}}
}
internal sealed class MarkForm : Form {
 readonly Point? point; readonly Rectangle target;
 public MarkForm(Rectangle rect,Point? hit){target=rect;point=hit;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=Rectangle.Inflate(rect,3,3);BackColor=Color.Magenta;TransparencyKey=Color.Magenta;DoubleBuffered=true;}
 protected override bool ShowWithoutActivation {get{return true;}}
 protected override CreateParams CreateParams {get{var value=base.CreateParams;value.ExStyle|=0x80000|0x20|0x08000000|0x80;return value;}}
 protected override void WndProc(ref Message value){if(value.Msg==0x84){value.Result=new IntPtr(-1);return;}base.WndProc(ref value);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Color.DeepSkyBlue,2)){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.DrawRectangle(pen,2,2,Math.Max(1,Width-5),Math.Max(1,Height-5));if(point.HasValue)e.Graphics.DrawEllipse(pen,point.Value.X-Left-14,point.Value.Y-Top-14,28,28);}using(var background=new SolidBrush(Color.FromArgb(20,30,42)))e.Graphics.FillRectangle(background,8,8,130,25);using(var color=new SolidBrush(Color.White))e.Graphics.DrawString("DSH 正在操作",SystemFonts.MessageBoxFont,color,12,11);}
}
internal static class Indicator {
 static readonly object sync=new object(); static Thread thread;static Control dispatcher;static MarkForm form;static System.Windows.Forms.Timer watchdog;static long shownAt;
 static void Ready(){lock(sync){if(thread!=null&&thread.IsAlive&&dispatcher!=null)return;var ready=new ManualResetEventSlim(false);thread=new Thread(()=>{Win.SetThreadDpiAwarenessContext(new IntPtr(-4));dispatcher=new Control();var handle=dispatcher.Handle;ready.Set();Application.Run();}){IsBackground=true,Name="DSH desktop marker"};thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!ready.Wait(2000))throw new Failure("indicator-unavailable","无法创建桌面可见标记");}}
 public static bool Show(Rectangle rect,Point? point){Ready();bool result=false;dispatcher.Invoke((Action)(()=>{watchdog?.Stop();watchdog?.Dispose();form?.Close();form?.Dispose();form=new MarkForm(rect,point);form.Show();Win.ShowWindow(form.Handle,8);form.Update();shownAt=Environment.TickCount64;result=Win.IsWindowVisible(form.Handle);watchdog=new System.Windows.Forms.Timer{Interval=30000};watchdog.Tick+=(_,__)=>Hide(false);watchdog.Start();}));return result;}
 public static IntPtr Handle {get{try{return form?.Handle??IntPtr.Zero;}catch{return IntPtr.Zero;}}}
 public static void Hide(bool fade=true){if(dispatcher==null||dispatcher.IsDisposed)return;dispatcher.BeginInvoke((Action)(()=>{watchdog?.Stop();watchdog?.Dispose();watchdog=null;var closing=form;form=null;if(closing==null)return;if(!fade){closing.Close();closing.Dispose();return;}Action fadeOut=()=>{var timer=new System.Windows.Forms.Timer{Interval=30};int step=0;timer.Tick+=(_,__)=>{if(closing.IsDisposed||++step>=10){timer.Stop();timer.Dispose();if(!closing.IsDisposed){closing.Close();closing.Dispose();}}else closing.Opacity=Math.Max(0,1-step/10.0);};timer.Start();};int remaining=(int)Math.Max(0,900-(Environment.TickCount64-shownAt));if(remaining>0){var delay=new System.Windows.Forms.Timer{Interval=remaining};delay.Tick+=(_,__)=>{delay.Stop();delay.Dispose();fadeOut();};delay.Start();}else fadeOut();}));}
}
public static class Host {
 static readonly BlockingCollection<Action> queue=new BlockingCollection<Action>();
 static readonly Thread worker;static string runsRoot,powershell;static bool initialized;
 static readonly Dictionary<string,Entry> refs=new Dictionary<string,Entry>();static readonly Dictionary<string,int> counters=new Dictionary<string,int>();
 static readonly HashSet<string> secrets=new HashSet<string>();static int generation;
 static Host(){worker=new Thread(()=>{Win.SetThreadDpiAwarenessContext(new IntPtr(-4));foreach(var task in queue.GetConsumingEnumerable())task();}){IsBackground=true,Name="DSH UI Automation"};worker.SetApartmentState(ApartmentState.MTA);worker.Start();}
 public static void Initialize(string root,string version){Win.SetProcessDpiAwarenessContext(new IntPtr(-4));Win.SetThreadDpiAwarenessContext(new IntPtr(-4));runsRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);Directory.CreateDirectory(runsRoot);ValidatePath(Path.Combine(runsRoot,"guard.tmp"));powershell=version;initialized=true;LoadSecrets();}
 public static object Call(string op,string input){var source=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);queue.Add(()=>{try{using(var doc=JsonDocument.Parse(input)){source.SetResult(Dispatch(op,doc.RootElement));}}catch(Exception error){source.SetException(error);}});return source.Task.GetAwaiter().GetResult();}
 static Dictionary<string,object> Data(params object[] entries){var result=new Dictionary<string,object>();for(int n=0;n<entries.Length;n+=2)result[(string)entries[n]]=entries[n+1];return result;}
 static string Text(JsonElement a,string key,string fallback=""){JsonElement v;return a.TryGetProperty(key,out v)&&v.ValueKind==JsonValueKind.String?v.GetString():fallback;}
 static bool Bool(JsonElement a,string key,bool fallback=false){JsonElement v;return a.TryGetProperty(key,out v)&&(v.ValueKind==JsonValueKind.True||v.ValueKind==JsonValueKind.False)?v.GetBoolean():fallback;}
 static int Int(JsonElement a,string key,int fallback=0){JsonElement v;return a.TryGetProperty(key,out v)&&v.ValueKind==JsonValueKind.Number?v.GetInt32():fallback;}
 static IntPtr Handle(string text){long value;if(text.StartsWith("0x",StringComparison.OrdinalIgnoreCase)){if(!Int64.TryParse(text.Substring(2),System.Globalization.NumberStyles.HexNumber,null,out value))throw new Failure("window-not-found","窗口句柄无效");}else if(!Int64.TryParse(text,out value))throw new Failure("window-not-found","窗口句柄无效");if(value<=0)throw new Failure("window-not-found","窗口句柄无效");return new IntPtr(value);}
 static void SafeDesktop(){var desktop=Win.OpenInputDesktop(0,false,1);try{var name=new StringBuilder(256);int needed;if(desktop==IntPtr.Zero||!Win.GetUserObjectInformation(desktop,2,name,512,out needed)||!String.Equals(name.ToString(),"Default",StringComparison.OrdinalIgnoreCase))throw new Failure("tier-denied","锁屏、UAC 和安全桌面不支持自动化");}finally{if(desktop!=IntPtr.Zero)Win.CloseDesktop(desktop);}}
 static WindowData Info(IntPtr handle){if(!Win.IsWindow(handle))throw new Failure("window-not-found","窗口已消失");uint pid;Win.GetWindowThreadProcessId(handle,out pid);var process=Process.GetProcessById((int)pid);string image;try{image=process.MainModule.FileName;}catch{throw new Failure("tier-denied","无法核对窗口进程身份");}var title=new StringBuilder(513);Win.GetWindowText(handle,title,title.Capacity);var name=new StringBuilder(129);Win.GetClassName(handle,name,name.Capacity);Win.RECT box;if(!Win.GetWindowRect(handle,out box))throw new Failure("window-not-found","无法读取窗口位置");var basename=Path.GetFileName(image);var windowKey=String.Join("|",handle.ToInt64().ToString(),(int)pid,process.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(),basename,name.ToString());var hiddenTitle=secrets.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(windowKey+"|title"))).ToLowerInvariant());var protectedName=new[]{"consent.exe","logonui.exe","winlogon.exe","lockapp.exe","credentialuibroker.exe"}.Contains(basename.ToLowerInvariant());return new WindowData{titleHidden=hiddenTitle,handle=handle.ToInt64().ToString(),pid=(int)pid,process=basename,imagePath=image,className=name.ToString(),title=title.ToString(),startedAt=process.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(),foreground=Win.GetForegroundWindow()==handle,minimized=Win.IsIconic(handle),secure=protectedName||name.ToString().IndexOf("Credential",StringComparison.OrdinalIgnoreCase)>=0,elevated=Win.Elevated((int)pid),rect=new Box{x=box.Left,y=box.Top,w=box.Right-box.Left,h=box.Bottom-box.Top}};}
 static WindowData Allowed(WindowData window){if(window.secure||window.elevated)throw new Failure("tier-denied","凭据、安全桌面和提升权限的窗口不支持自动化");return window;}
 static List<WindowData> Windows(string filter,bool visible){var result=new List<WindowData>();Win.EnumWindows((handle,_)=>{try{if(visible&&!Win.IsWindowVisible(handle))return true;uint pid;Win.GetWindowThreadProcessId(handle,out pid);if(pid==(uint)Environment.ProcessId)return true;if(!String.IsNullOrEmpty(filter)){var process=Process.GetProcessById((int)pid);if(!String.Equals(process.ProcessName+".exe",filter,StringComparison.OrdinalIgnoreCase))return true;}var row=Info(handle);if(!String.IsNullOrWhiteSpace(row.title)||!visible)result.Add(row);}catch{}return result.Count<200;},IntPtr.Zero);return result;}
 static WindowData Resolve(JsonElement selector){if(selector.ValueKind==JsonValueKind.Number)return Info(new IntPtr(selector.GetInt64()));string process="",title="";if(selector.ValueKind==JsonValueKind.String){var value=selector.GetString();long number;if(value.StartsWith("0x",StringComparison.OrdinalIgnoreCase)||Int64.TryParse(value,out number))return Info(Handle(value));process=value;}else if(selector.ValueKind==JsonValueKind.Object){process=Text(selector,"process");title=Text(selector,"title");}else throw new Failure("window-not-found","窗口选择器无效");var rows=Windows(process,true).Where(row=>String.IsNullOrEmpty(title)||row.title.IndexOf(title,StringComparison.OrdinalIgnoreCase)>=0).ToArray();if(rows.Length!=1)throw new Failure("window-not-found",rows.Length==0?"没有匹配的窗口":"多个窗口匹配，请使用明确句柄");return rows[0];}
 static WindowData Bound(JsonElement args){var window=Allowed(Info(Handle(Text(args,"handle"))));var expected=Text(args,"expectedWindow");if(!String.IsNullOrEmpty(expected)&&window.Key!=expected)throw new Failure("element-gone","窗口句柄或进程已变化");return window;}
 static string Runtime(AutomationElement e){return String.Join(".",e.GetRuntimeId());}
 static string Role(AutomationElement e){return e.Current.ControlType.ProgrammaticName.Replace("ControlType.","");}
 static string[] Patterns(AutomationElement e){var list=new List<string>();object value;foreach(var item in new[]{Tuple.Create("invoke",InvokePattern.Pattern),Tuple.Create("select",SelectionItemPattern.Pattern),Tuple.Create("toggle",TogglePattern.Pattern),Tuple.Create("expand",ExpandCollapsePattern.Pattern),Tuple.Create("value",ValuePattern.Pattern),Tuple.Create("text",TextPattern.Pattern)})if(e.TryGetCurrentPattern(item.Item2,out value))list.Add(item.Item1);return list.ToArray();}
 static NodeData Describe(AutomationElement e,WindowData window,string reference){var info=e.Current;var runtime=Runtime(e);bool hidden=info.IsPassword||secrets.Contains(SecretKey(window,runtime))||secrets.Contains(SecretKey(window,"window"));var box=info.BoundingRectangle;return new NodeData{@ref=reference,runtimeId=runtime,windowIdentity=window.Key,role=info.ControlType.ProgrammaticName.Replace("ControlType.",""),name=hidden?(info.IsPassword?"[密码框]":"[保密字段]"):window.titleHidden&&info.ControlType==ControlType.Window?"[保密输入窗口]":Cut(info.Name,512),automationId=Cut(info.AutomationId,256),className=Cut(info.ClassName,128),enabled=info.IsEnabled,offscreen=info.IsOffscreen,password=info.IsPassword,valueHidden=hidden,rect=new Box{x=(int)box.X,y=(int)box.Y,w=(int)box.Width,h=(int)box.Height},patterns=Patterns(e)};}
 static string Cut(string value,int length){return value==null?"":value.Length>length?value.Substring(0,length):value;}
 static void Release(string owner){foreach(var key in refs.Where(row=>row.Value.Owner==owner).Select(row=>row.Key).ToArray())refs.Remove(key);}
 static object Snapshot(JsonElement args){var window=Bound(args);string owner=Text(args,"owner"),prefix=Text(args,"prefix");if(!Guid.TryParse(owner,out var parsed)||prefix.Length!=8||prefix.Any(c=>!Uri.IsHexDigit(c)))throw new Failure("element-gone","会话引用身份无效");LoadSecrets();Release(owner);var nodes=new List<NodeData>();var walker=TreeWalker.ControlViewWalker;var stack=new Stack<Tuple<AutomationElement,int>>();stack.Push(Tuple.Create(AutomationElement.FromHandle(Handle(window.handle)),0));int depth=Int(args,"depth",12),limit=Math.Min(800,Int(args,"maxNodes",120));bool truncated=false;var clock=Stopwatch.StartNew();while(stack.Count>0){if(nodes.Count>=limit||clock.ElapsedMilliseconds>Int(args,"maxMillis",1500)){truncated=true;break;}var current=stack.Pop();try{var element=current.Item1;var info=element.Current;if(info.IsOffscreen)continue;var patterns=Patterns(element);var hidden=info.IsPassword||secrets.Contains(SecretKey(window,Runtime(element)))||secrets.Contains(SecretKey(window,"window"));var name=hidden?"[保密字段]":info.Name;bool interactive=patterns.Length>0||new[]{ControlType.Edit,ControlType.Button,ControlType.CheckBox,ControlType.ComboBox,ControlType.MenuItem,ControlType.ListItem,ControlType.TreeItem}.Contains(info.ControlType);if(info.IsControlElement&&(!Bool(args,"interactiveOnly")||interactive)&&(!Bool(args,"onlyNamed",true)||!String.IsNullOrWhiteSpace(name)||interactive)){int count;counters.TryGetValue(owner,out count);if(++count>1000000)throw new Failure("artifact-limit","会话引用数量已达到上限");counters[owner]=count;var reference="["+prefix+":"+count+"]";var row=Describe(element,window,reference);refs[owner+"|"+reference]=new Entry{Element=element,Runtime=row.runtimeId,Role=row.role,Name=info.Name,AutomationId=info.AutomationId,Owner=owner,Window=window};nodes.Add(row);}if(hidden)continue;if(current.Item2>=depth){if(walker.GetFirstChild(element)!=null)truncated=true;continue;}var children=new List<AutomationElement>();var child=walker.GetFirstChild(element);while(child!=null&&children.Count<800){children.Add(child);child=walker.GetNextSibling(child);}if(child!=null)truncated=true;for(int n=children.Count-1;n>=0;n--)stack.Push(Tuple.Create(children[n],current.Item2+1));}catch(ElementNotAvailableException){truncated=true;}}return Data("handle",window.handle,"pid",window.pid,"process",window.process,"className",window.className,"title",window.title,"startedAt",window.startedAt,"titleHidden",window.titleHidden,"generation",++generation,"nodes",nodes,"truncated",truncated);}
 static Entry Target(JsonElement args){string owner=Text(args,"owner"),reference=Text(args,"ref");Entry entry;if(!refs.TryGetValue(owner+"|"+reference,out entry))throw new Failure("element-gone","引用已失效，请重新快照");var window=Allowed(Info(Handle(Text(args,"handle"))));if(window.Key!=entry.Window.Key||window.title!=entry.Window.title)throw new Failure("element-gone","引用窗口已变化");try{var e=entry.Element;var info=e.Current;if(Runtime(e)!=entry.Runtime||Role(e)!=entry.Role||info.Name!=entry.Name||info.AutomationId!=entry.AutomationId)throw new Failure("element-gone","控件已变化，请重新快照");if(!info.IsEnabled||info.IsOffscreen)throw new Failure("action-failed","控件已停用或不可见");return entry;}catch(ElementNotAvailableException){throw new Failure("element-gone","控件已消失，请重新快照");}}
 static object Invoke(JsonElement args){var entry=Target(args);object pattern;var method=Text(args,"method");switch(method){case "invoke":if(!entry.Element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))throw Unsupported();((InvokePattern)pattern).Invoke();break;case "select":if(!entry.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern,out pattern))throw Unsupported();((SelectionItemPattern)pattern).Select();break;case "toggle":if(!entry.Element.TryGetCurrentPattern(TogglePattern.Pattern,out pattern))throw Unsupported();((TogglePattern)pattern).Toggle();break;case "expand":if(!entry.Element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern,out pattern))throw Unsupported();((ExpandCollapsePattern)pattern).Expand();break;default:throw Unsupported();}return Data("method",method,"ref",Text(args,"ref"),"performed",true,"verification","refresh-required","physical",false);}
 static Failure Unsupported(){return new Failure("unsupported-action","控件不支持此语义动作；未自动重放为物理动作");}
 static void Protect(Entry entry){bool changed=secrets.Add(SecretKey(entry.Window,entry.Runtime));changed=secrets.Add(SecretKey(entry.Window,"title"))||changed;if(changed)SaveSecrets();}
 static object SetValue(JsonElement args){var entry=Target(args);object pattern;if(!entry.Element.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))throw Unsupported();var value=(ValuePattern)pattern;if(value.Current.IsReadOnly)throw new Failure("input-denied","输入控件只读");bool secret=Bool(args,"secret");if(entry.Element.Current.IsPassword&&!secret)throw new Failure("input-denied","密码框需要保密输入");var text=Text(args,"text");if(text.Length>65536)throw new Failure("input-denied","输入内容过大");if(secret)Protect(entry);if(!Bool(args,"clear",true)){if(secret||entry.Element.Current.IsPassword)throw new Failure("input-denied","保密输入不支持追加读回");text=value.Current.Value+text;}value.SetValue(text);entry.Name=entry.Element.Current.Name;bool verified=false;if(!secret&&!entry.Element.Current.IsPassword){verified=value.Current.Value==text;if(!verified)throw new Failure("action-failed","输入已送出，但读回结果不一致，请检查控件");}return Data("ref",Text(args,"ref"),"performed",true,"verified",verified,"verification",secret?"hidden":"read-back","physical",false);}
 static object ReadValue(JsonElement args){var entry=Target(args);if(entry.Element.Current.IsPassword||secrets.Contains(SecretKey(entry.Window,entry.Runtime))||secrets.Contains(SecretKey(entry.Window,"window")))throw new Failure("input-denied","保密字段不返回值");object pattern;if(!entry.Element.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))throw Unsupported();var text=((ValuePattern)pattern).Current.Value;return Data("ref",Text(args,"ref"),"value",Cut(text,300),"truncated",text.Length>300);}
 static WindowData Focus(WindowData window){var handle=Handle(window.handle);if(window.minimized)Win.ShowWindow(handle,9);if(Win.GetForegroundWindow()!=handle)Win.SetForegroundWindow(handle);Thread.Sleep(50);if(Win.GetForegroundWindow()!=handle)throw new Failure("input-denied","无法将目标窗口置于前台，请用户先切换窗口");return Info(handle);}
 static void PhysicalCheck(WindowData window,string expected,Box before){var fresh=Allowed(Info(Handle(window.handle)));if(fresh.Key!=expected||fresh.title!=window.title||!fresh.foreground||fresh.rect.x!=before.x||fresh.rect.y!=before.y||fresh.rect.w!=before.w||fresh.rect.h!=before.h)throw new Failure("action-failed","窗口身份、位置或前台发生变化，停止真实键鼠操作");}
 static void Prepare(WindowData window,Point? point,JsonElement args,Entry entry){if(!Win.AreDpiAwarenessContextsEqual(Win.GetThreadDpiAwarenessContext(),new IntPtr(-4)))throw new Failure("input-denied","DPI 坐标校验失败");window=Focus(window);if(entry!=null)entry.Element.SetFocus();if(point.HasValue){if(!window.rect.GetRectangle().Contains(point.Value))throw new Failure("input-denied","点击坐标超出窗口");var root=Win.GetAncestor(Win.WindowFromPoint(new Win.POINT(point.Value.X,point.Value.Y)),2);if(root!=Handle(window.handle))throw new Failure("action-failed","目标位置被其他窗口遮挡");}uint activity=Win.LastInput();bool shown=false;try{shown=Indicator.Show(window.rect.GetRectangle(),point);}catch{if(Bool(args,"requireIndicator",true))throw new Failure("indicator-unavailable","无法显示桌面操作标记，未执行真实键鼠动作");}if(!shown&&Bool(args,"requireIndicator",true))throw new Failure("indicator-unavailable","操作标记不可见，未执行真实键鼠动作");Thread.Sleep(Math.Max(600,Math.Min(1000,Int(args,"holdMs",600))));SafeDesktop();PhysicalCheck(window,Text(args,"expectedWindow",window.Key),window.rect);if(Win.LastInput()!=activity)throw new Failure("input-denied","标记显示期间检测到用户键鼠输入，已停止本次物理操作");if(entry!=null)Target(args);}
 static Win.INPUT Key(ushort code,bool up=false){return new Win.INPUT{type=1,value=new Win.UNION{key=new Win.KEYBOARD{vk=code,flags=up?2u:0u}}};}
 static void Send(Win.INPUT[] values){if(Win.SendInput((uint)values.Length,values,Marshal.SizeOf<Win.INPUT>())!=values.Length)throw new Failure("action-failed","真实输入未全部发送，可能已有部分效果，请检查当前窗口");}
 static object PointInput(JsonElement args){var window=Bound(args);Entry entry=String.IsNullOrEmpty(Text(args,"ref"))?null:Target(args);var pointValue=args.GetProperty("point");var point=new Point(Int(pointValue,"x"),Int(pointValue,"y"));try{Prepare(window,point,args,entry);var screen=SystemInformation.VirtualScreen;int x=(int)Math.Round((point.X-screen.X)*65535.0/Math.Max(1,screen.Width-1)),y=(int)Math.Round((point.Y-screen.Y)*65535.0/Math.Max(1,screen.Height-1));var move=new Win.INPUT{type=0,value=new Win.UNION{mouse=new Win.MOUSE{x=x,y=y,flags=0x8000|0x4000|1}}};var down=new Win.INPUT{type=0,value=new Win.UNION{mouse=new Win.MOUSE{flags=2}}};var up=new Win.INPUT{type=0,value=new Win.UNION{mouse=new Win.MOUSE{flags=4}}};Send(new[]{move,down,up});if(Win.GetForegroundWindow()!=Handle(window.handle))throw new Failure("action-failed","点击已送出，前台已变化；请检查结果，未重放动作");return Data("performed",true,"physical",true,"verification","refresh-required");}finally{Indicator.Hide();}}
 static object Keys(JsonElement args){var window=Bound(args);Entry entry=String.IsNullOrEmpty(Text(args,"ref"))?null:Target(args);bool secret=Bool(args,"secret");var text=Text(args,"text");if(text.Length>65536)throw new Failure("input-denied","输入内容过大");if(entry!=null&&entry.Element.Current.IsPassword&&!secret)throw new Failure("input-denied","密码框需要保密输入");if(secret){if(entry!=null)Protect(entry);else ProtectFocused(window);}try{Prepare(window,null,args,entry);if(Bool(args,"clear",true)&&args.TryGetProperty("text",out var supplied))Send(new[]{Key(0x11),Key(0x41),Key(0x41,true),Key(0x11,true),Key(0x08),Key(0x08,true)});for(int start=0;start<text.Length;start+=128){if(Win.GetForegroundWindow()!=Handle(window.handle))throw new Failure("action-failed","输入期间前台变化，已停止；可能已有部分输入");var values=new List<Win.INPUT>();foreach(char letter in text.Substring(start,Math.Min(128,text.Length-start))){values.Add(new Win.INPUT{type=1,value=new Win.UNION{key=new Win.KEYBOARD{scan=letter,flags=4}}});values.Add(new Win.INPUT{type=1,value=new Win.UNION{key=new Win.KEYBOARD{scan=letter,flags=6}}});}Send(values.ToArray());}if(Bool(args,"submit")){if(Win.GetForegroundWindow()!=Handle(window.handle))throw new Failure("action-failed","提交前前台变化，已停止");Send(new[]{Key(0x0D),Key(0x0D,true)});}if(Win.GetForegroundWindow()!=Handle(window.handle))throw new Failure("action-failed","输入已送出，前台已变化；请检查结果");if(entry!=null)entry.Name=entry.Element.Current.Name;return Data("performed",true,"physical",true,"verification",secret?"hidden":"refresh-required");}finally{Indicator.Hide();}}
 static void ValidatePath(string file){var target=Path.GetFullPath(file);if(!target.StartsWith(runsRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Failure("artifact-denied","产物路径越出 runs 目录");for(var dir=Path.GetDirectoryName(target);dir!=null;dir=Path.GetDirectoryName(dir)){if(Directory.Exists(dir)&&(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new Failure("artifact-denied","产物目录不允许链接");if(String.Equals(dir,runsRoot,StringComparison.OrdinalIgnoreCase))break;}if(File.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new Failure("artifact-denied","产物文件不允许链接");}
 static object Screenshot(JsonElement args){LoadSecrets();var window=Bound(args);if(window.minimized)throw new Failure("unsupported-action","请用户恢复目标窗口后截图");var rect=window.rect;if(rect.w<1||rect.h<1||rect.w>16384||rect.h>16384||(long)rect.w*rect.h>32000000)throw new Failure("artifact-limit","目标窗口截图面积超出上限");var crop=new Rectangle(0,0,rect.w,rect.h);if(args.TryGetProperty("rect",out var requested)&&requested.ValueKind==JsonValueKind.Object)crop=new Rectangle(Int(requested,"x"),Int(requested,"y"),Int(requested,"w"),Int(requested,"h"));if(crop.Width<=0||crop.Height<=0||!new Rectangle(0,0,rect.w,rect.h).Contains(crop))throw new Failure("input-denied","裁剪区域必须在目标窗口内");var file=Text(args,"path");ValidatePath(file);int maxBytes=Int(args,"maxBytes",5242880),maxWidth=Int(args,"maxWidth",3200),maxHeight=Int(args,"maxHeight",2400);if(maxBytes<16384||maxBytes>20971520)throw new Failure("artifact-limit","截图大小限制无效");using(var original=new Bitmap(rect.w,rect.h,PixelFormat.Format32bppArgb)){using(var graphics=Graphics.FromImage(original)){var hdc=graphics.GetHdc();try{if(!Win.PrintWindow(Handle(window.handle),hdc,2))throw new Failure("unsupported-action","此窗口不支持独立窗口截图；未截取其他窗口或全屏");}finally{graphics.ReleaseHdc(hdc);}}var latest=Allowed(Info(Handle(window.handle)));if(latest.Key!=window.Key||latest.title!=window.title||!latest.rect.GetRectangle().Equals(window.rect.GetRectangle()))throw new Failure("element-gone","截图期间窗口身份或位置变化，请重新截图");bool redacted=Redact(original,window);double ratio=Math.Min(1,Math.Min(maxWidth/(double)crop.Width,maxHeight/(double)crop.Height));for(int attempt=0;attempt<12;attempt++){int width=Math.Max(1,(int)Math.Round(crop.Width*ratio)),height=Math.Max(1,(int)Math.Round(crop.Height*ratio));using(var image=new Bitmap(width,height,PixelFormat.Format32bppArgb)){using(var graphics=Graphics.FromImage(image)){graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.DrawImage(original,new Rectangle(0,0,width,height),crop,GraphicsUnit.Pixel);}using(var bytes=new MemoryStream()){image.Save(bytes,ImageFormat.Png);if(bytes.Length<=maxBytes){ValidatePath(file);using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))bytes.WriteTo(output);return Data("path",file,"width",width,"height",height,"bytes",bytes.Length,"redacted",redacted,"captureRect",new Box{x=rect.x+crop.X,y=rect.y+crop.Y,w=crop.Width,h=crop.Height});}}}ratio*=0.75;}}throw new Failure("artifact-limit","截图缩放后仍超出限制");}
 static string SecretKey(WindowData window,string element){return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(window.Key+"|"+element))).ToLowerInvariant();}
 static void LoadSecrets(){
  var file=Path.Combine(runsRoot,"secret-elements.json");ValidatePath(file);
  if(!File.Exists(file))return;
  if(new FileInfo(file).Length>1048576)throw new Failure("artifact-denied","保密字段索引过大");
  foreach(var value in JsonSerializer.Deserialize<string[]>(File.ReadAllText(file))??Array.Empty<string>())
   if(value!=null&&value.Length==64&&value.All(Uri.IsHexDigit))secrets.Add(value);
  if(secrets.Count>8192)throw new Failure("artifact-limit","保密字段索引已达到上限");
 }
 static void SaveSecrets(){
  LoadSecrets();if(secrets.Count>8192)throw new Failure("artifact-limit","保密字段索引已达到上限");
  var file=Path.Combine(runsRoot,"secret-elements.json");var temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  ValidatePath(file);ValidatePath(temporary);
  using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))JsonSerializer.Serialize(output,secrets);
  try{File.Move(temporary,file,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 static void ProtectFocused(WindowData window){
  secrets.Add(SecretKey(window,"title"));
  var focused=AutomationElement.FocusedElement;
  if(focused!=null&&focused.Current.ProcessId==window.pid&&
     (focused.Current.ControlType==ControlType.Edit||Patterns(focused).Contains("value"))){
   var ancestor=focused;bool bound=false;
   for(int depth=0;ancestor!=null&&depth<40;depth++,ancestor=TreeWalker.ControlViewWalker.GetParent(ancestor))
    if(ancestor.Current.NativeWindowHandle==Handle(window.handle).ToInt64()){bound=true;break;}
   if(bound){secrets.Add(SecretKey(window,Runtime(focused)));SaveSecrets();return;}
  }
  // A custom-drawn input cannot be located reliably. Hide this window's contents thereafter.
  secrets.Add(SecretKey(window,"window"));SaveSecrets();
 }
 static bool Redact(Bitmap image,WindowData window){
  var whole=new Rectangle(0,0,image.Width,image.Height);var areas=new List<Rectangle>();
  if(window.titleHidden){uint dpi=Win.GetDpiForWindow(Handle(window.handle));int caption=Win.GetSystemMetricsForDpi(4,dpi),frame=Win.GetSystemMetricsForDpi(33,dpi),padding=Win.GetSystemMetricsForDpi(92,dpi);int height=caption>0?caption+frame+padding+2:96;areas.Add(new Rectangle(0,0,image.Width,Math.Min(image.Height,height)));}
  if(secrets.Contains(SecretKey(window,"window")))areas.Add(whole);
  else{
   var stack=new Stack<AutomationElement>();stack.Push(AutomationElement.FromHandle(Handle(window.handle)));
   var walker=TreeWalker.ControlViewWalker;var clock=Stopwatch.StartNew();int count=0;
   while(stack.Count>0){
    if(++count>800||clock.ElapsedMilliseconds>1500){areas.Clear();areas.Add(whole);break;}
    var element=stack.Pop();
    try{
     var info=element.Current;
     if(info.IsOffscreen)continue;
     if(info.IsPassword||secrets.Contains(SecretKey(window,Runtime(element)))){
      var rect=info.BoundingRectangle;
      if(rect.IsEmpty||Double.IsInfinity(rect.Width)||Double.IsInfinity(rect.Height)){areas.Clear();areas.Add(whole);break;}
      var area=new Rectangle((int)rect.X-window.rect.x-2,(int)rect.Y-window.rect.y-2,(int)rect.Width+4,(int)rect.Height+4);
      areas.Add(Rectangle.Intersect(area,whole));continue;
     }
     var child=walker.GetFirstChild(element);int children=0;
     while(child!=null){if(++children>800){areas.Clear();areas.Add(whole);stack.Clear();break;}stack.Push(child);child=walker.GetNextSibling(child);}
    }catch(ElementNotAvailableException){areas.Clear();areas.Add(whole);break;}
   }
  }
  if(areas.Count==0)return false;
  using(var graphics=Graphics.FromImage(image))using(var brush=new SolidBrush(Color.FromArgb(16,24,32)))
   foreach(var area in areas)if(area.Width>0&&area.Height>0)graphics.FillRectangle(brush,area);
  return true;
 }
 static object Dispatch(string op,JsonElement args){if(!initialized)throw new Failure("engine-unavailable","桌面引擎尚未初始化");if(op=="hello"){var monitors=new List<object>();foreach(var screen in Screen.AllScreens){uint dx,dy;var point=new Win.POINT(screen.Bounds.X+screen.Bounds.Width/2,screen.Bounds.Y+screen.Bounds.Height/2);int status=Win.GetDpiForMonitor(Win.MonitorFromPoint(point,2),0,out dx,out dy);monitors.Add(Data("w",screen.Bounds.Width,"h",screen.Bounds.Height,"x",screen.Bounds.X,"y",screen.Bounds.Y,"scale",status==0?dx/96.0:1.0));}return Data("protocol",1,"dpiAware",Win.AreDpiAwarenessContextsEqual(Win.GetThreadDpiAwarenessContext(),new IntPtr(-4)),"monitors",monitors,"powershell",powershell,"uiaVersion",typeof(AutomationElement).Assembly.GetName().Version.ToString());}if(op=="ping")return Data("ok",true,"uptimeMs",Environment.TickCount64);if(op=="session.release"){Release(Text(args,"owner"));return Data("released",true);}if(op=="indicator.hide"){Indicator.Hide();return Data("hidden",true);}SafeDesktop();LoadSecrets();using(var mutex=new Mutex(false,"Local\\DSH.Desktop.Input")){bool entered=false;try{try{entered=mutex.WaitOne(0);}catch(AbandonedMutexException){entered=true;}if(!entered)throw new Failure("desktop-busy","另一个桌面操作正在执行");switch(op){case "windows.list":return Data("windows",Windows(Text(args,"process"),Bool(args,"onlyVisible",true)),"truncated",false);case "window.info":return Resolve(args.GetProperty("selector"));case "window.snapshot":return Snapshot(args);case "window.focus":return Focus(Bound(args));case "window.screenshot":return Screenshot(args);case "element.info":{var entry=Target(args);return Describe(entry.Element,entry.Window,Text(args,"ref"));}case "element.invoke":return Invoke(args);case "element.setValue":return SetValue(args);case "element.readValue":return ReadValue(args);case "input.point":return PointInput(args);case "input.keys":return Keys(args);case "system.idle":return Data("idleMs",unchecked((uint)Environment.TickCount)-Win.LastInput());case "indicator.show":{var markerRect=args.TryGetProperty("handle",out _)?Bound(args).rect.GetRectangle():new Rectangle(Screen.PrimaryScreen.WorkingArea.Left+12,Screen.PrimaryScreen.WorkingArea.Top+12,170,48);var shown=Indicator.Show(markerRect,null);if(!shown)throw new Failure("indicator-unavailable","桌面标记不可见");return Data("shown",true);}default:throw new Failure("unsupported-action","未声明的桌面协议操作");}}finally{if(entered)mutex.ReleaseMutex();}}}
}
}
