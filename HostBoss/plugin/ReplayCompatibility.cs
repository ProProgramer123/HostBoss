using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Kolkata.UI;

namespace HostBoss;

// Match the native review order: preload, wait for recording receipts, freeze
// evidence, request boss notes, then prepare the selected clips.
internal static class ReplayCompatibility
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type[] Signature = { typeof(ReviewDayOverview), typeof(CancellationToken) };
    private static readonly MethodInfo LegacyWait = typeof(ReviewReplayService).GetMethod("WaitForMetadata", Flags, null, Signature, null);
    private static readonly MethodInfo BeginPreload = typeof(ReviewReplayService).GetMethod("BeginPreloadHighlights", Flags, null, Signature, null);
    private static readonly MethodInfo WaitForRecordings = typeof(ReviewReplayService).GetMethod("WaitForHighlightRecordings", Flags, null, Signature, null);

    internal static void Validate()
    {
        if (LegacyWait == null && BeginPreload == null)
            throw new NotSupportedException("HostBoss: unsupported review replay API. Update HostBoss for this game build.");
    }

    internal static Task PrepareMetadata(ReviewReplayService service, ReviewDayOverview overview, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Validate();
        try
        {
            if (LegacyWait != null)
                return (Task)LegacyWait.Invoke(service, new object[] { overview, token });
            BeginPreload.Invoke(service, new object[] { overview, token });
            if (WaitForRecordings != null)
                return (Task)WaitForRecordings.Invoke(service, new object[] { overview, token });
            // Earlier builds have no separate receipt wait.
            return Task.CompletedTask;
        }
        catch (TargetInvocationException e)
        {
            if (e.InnerException != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
