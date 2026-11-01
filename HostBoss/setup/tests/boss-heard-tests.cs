using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HostBoss;
using Kolkata.UI;
class BossHeardTests {
 static string root; static string plugin;
 static int Main(string[] args) {
  root=args[0]; plugin=args[1];
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{
   string name=new AssemblyName(e.Name).Name+".dll";
   foreach(string dir in new[]{plugin,"Scam With Your Friends_Data/Managed","BepInEx/core"}) {
    string file=Path.Combine(root,dir,name);if(File.Exists(file))return Assembly.LoadFrom(file);
   }
   return null;
  };
  try {Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 [MethodImpl(MethodImplOptions.NoInlining)] static void Run(){
  var draft=new ReviewSlideDeckDefinition {slides=new[]{new ReviewSlideDefinition{id="custom",layout="checklist",title="What the boss heard"}}};
  var native=new ReviewSlideDeckDefinition {slides=new[]{
   new ReviewSlideDefinition{id="runtime-ai-boss-notes-0",layout="checklist",narration="Original notes",replayMoments=new[]{new ReviewReplayMoment{replayId="clip-1",quote="Native quote",introduction="Original intro",reaction="Original reaction"}}},
   new ReviewSlideDefinition{id="runtime-ai-boss-notes-1",layout="checklist",narration="Next notes"}
  }};
  var options=new PresentationOptions();options.For("custom").NativeHeard=true;options.For("custom").Camera="top-left";
  var deck=SlideDocument.ResolveNativeContent(draft,native,options);
  Check(deck.slides.Length==2&&deck.slides[1].id=="runtime-ai-boss-notes-1","all native boss-heard slides replace the placeholder");
  Check(deck.slides[0].replayMoments[0].replayId=="clip-1"&&deck.slides[0].replayMoments[0].introduction=="Original intro","recording identifiers and original speech survive custom deck resolution");
  Check(options.For(deck.slides[0].id).Camera=="top-left","camera option survives native expansion");
  options.NativeTts=false;deck=SlideDocument.ResolveNativeContent(draft,native,options);
  Check(deck.slides[0].narration==""&&deck.slides[0].replayMoments[0].reaction==""&&deck.slides[0].replayMoments[0].replayId=="clip-1","TTS toggle silences boss speech while preserving the recording");
  deck=SlideDocument.ResolveNativeContent(draft,new ReviewSlideDeckDefinition{slides=new[]{new ReviewSlideDefinition{id="opening",layout="quote"}}},options);
  Check(deck.slides.Length==1&&deck.slides[0].quote.Contains("No boss notes"),"no eligible moments produces an explicit empty result");
  Check(native.slides[0].replayMoments[0].reaction=="Original reaction","resolution does not mutate original review evidence");
 }
}
