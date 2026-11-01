using System;
using System.Threading;
using System.Threading.Tasks;
namespace Kolkata.UI {
public class ReviewDayOverview { }
public class ReviewReplayService {
 public int Calls; public bool Fail; public readonly TaskCompletionSource<bool> Pending=new TaskCompletionSource<bool>();
#if LEGACY
 public Task WaitForMetadata(ReviewDayOverview overview,CancellationToken token) {Calls++;if(Fail)throw new InvalidOperationException("fixture failure");return Pending.Task;}
#elif MODERN || RECEIPTS
 internal void BeginPreloadHighlights(ReviewDayOverview overview,CancellationToken token) {Calls++;if(Fail)throw new InvalidOperationException("fixture failure");}
#if RECEIPTS
 internal Task WaitForHighlightRecordings(ReviewDayOverview overview,CancellationToken token) {if(Calls!=1)throw new Exception("preload must precede receipt wait");return Pending.Task;}
#endif
#endif
}
}
class ReplayTests {
 static void Check(bool value,string message) {if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static int Main() {try{
 var service=new Kolkata.UI.ReviewReplayService();var overview=new Kolkata.UI.ReviewDayOverview();
#if UNSUPPORTED
 bool rejected=false;try {HostBoss.ReplayCompatibility.Validate();}catch(NotSupportedException){rejected=true;}Check(rejected,"unsupported replay API rejected clearly");
#else
 HostBoss.ReplayCompatibility.Validate();var task=HostBoss.ReplayCompatibility.PrepareMetadata(service,overview,CancellationToken.None);
 Check(service.Calls==1,"metadata preparation invoked once");
#if LEGACY || RECEIPTS
 Check(object.ReferenceEquals(task,service.Pending.Task)&&!task.IsCompleted,"recording readiness remains awaited before evidence selection");service.Pending.SetResult(true);task.GetAwaiter().GetResult();
#else
 Check(task.IsCompleted,"modern preloading is nonblocking; clip readiness remains owned by PrepareHighlights");
#endif
 var cancel=new CancellationTokenSource();cancel.Cancel();bool canceled=false;try{HostBoss.ReplayCompatibility.PrepareMetadata(service,overview,cancel.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled&&service.Calls==1,"cancellation prevents preloading");
 service.Fail=true;bool failed=false;try{HostBoss.ReplayCompatibility.PrepareMetadata(service,overview,CancellationToken.None);}catch(InvalidOperationException e){failed=e.Message=="fixture failure";}Check(failed,"native error is unwrapped and preserved");
#endif
 return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
