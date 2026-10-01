using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace BIManage.Infrastructure.Api
{
    /// <summary>
    /// Shares the server's verdict on a model's registration with the model-session sync.
    ///
    /// The model-session POST and the register POST are both kicked off from DocumentOpened
    /// and race each other. When the server refuses the registration (4xx — e.g. the company
    /// is outside the project's collaboration chain) no model-session call may go out for
    /// that model, so the session sync waits here for the verdict before posting.
    /// </summary>
    public static class ModelRegistrationGate
    {
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _verdicts =
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>(StringComparer.OrdinalIgnoreCase);

        // Sticky until the server accepts the model — outlives Reset() so the "Closed"
        // status update fired while the rejected document closes is suppressed too.
        private static readonly ConcurrentDictionary<string, byte> _rejected =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Records the outcome of the registration check for the current open of a model.
        /// </summary>
        public static void Publish(string? modelGuid, bool rejected)
        {
            if (string.IsNullOrEmpty(modelGuid)) return;

            if (rejected) _rejected[modelGuid!] = 0;
            else _rejected.TryRemove(modelGuid!, out _);

            _verdicts.GetOrAdd(modelGuid!, _ => NewVerdict()).TrySetResult(rejected);
        }

        /// <summary>
        /// Waits for the registration verdict of the current open. Returns true when the
        /// server rejected the model. On timeout falls back to the last known state so a
        /// registration check that never reports can't block session sync forever.
        /// </summary>
        public static async Task<bool> WaitIsRejectedAsync(string? modelGuid, TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(modelGuid)) return false;

            var verdict = _verdicts.GetOrAdd(modelGuid!, _ => NewVerdict());
            var completed = await Task.WhenAny(verdict.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return completed == verdict.Task ? verdict.Task.Result : IsRejected(modelGuid);
        }

        /// <summary>
        /// True when the server's last answer for this model was a rejection.
        /// </summary>
        public static bool IsRejected(string? modelGuid)
        {
            return !string.IsNullOrEmpty(modelGuid) && _rejected.ContainsKey(modelGuid!);
        }

        /// <summary>
        /// Forgets the verdict when the document closes so the next open waits for a fresh one.
        /// </summary>
        public static void Reset(string? modelGuid)
        {
            if (string.IsNullOrEmpty(modelGuid)) return;
            _verdicts.TryRemove(modelGuid!, out _);
        }

        private static TaskCompletionSource<bool> NewVerdict()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
