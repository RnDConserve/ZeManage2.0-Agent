using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BIManage.Infrastructure.Logging;
using BIManage.Infrastructure.SignalR.Messages;

namespace BIManage.Infrastructure.SignalR.Listeners
{
    /// <summary>
    /// Listens for server-pushed CollaborationWarning messages — the caller's company works on a
    /// project but is outside the collaboration network that just formed on it.
    ///
    /// The listener only extracts the project id and message; what to do with an open model on
    /// that project (popup, then close the model) is decided by the handler passed in, which
    /// owns the Revit documents.
    /// </summary>
    public class CollaborationWarningListener : SignalListenerBase
    {
        private readonly Action<string?, string?> _onWarning;

        public override string Name => "CollaborationWarning";

        public override IEnumerable<string> SupportedMethods => new[]
        {
            SignalRMethods.CollaborationWarning
        };

        /// <param name="onWarning">Called with (projectId, message) for every warning received.</param>
        public CollaborationWarningListener(Action<string?, string?> onWarning, ILogger logger) : base(logger)
        {
            _onWarning = onWarning ?? throw new ArgumentNullException(nameof(onWarning));
        }

        protected override Task ProcessMessageAsync(SignalRMessageInfo message)
        {
            LogMessageReceived(message);
            _logger?.LogInfo($"[CollaborationWarning] Payload: {message.Payload}");

            string? projectId = null;
            string? text = null;
            try
            {
                if (!string.IsNullOrEmpty(message.Payload))
                {
                    using var doc = JsonDocument.Parse(message.Payload);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        // Server serializes camelCase; match case-insensitively to be safe.
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Value.ValueKind != JsonValueKind.String) continue;

                            if (string.Equals(prop.Name, "projectId", StringComparison.OrdinalIgnoreCase))
                                projectId = prop.Value.GetString();
                            else if (string.Equals(prop.Name, "message", StringComparison.OrdinalIgnoreCase))
                                text = prop.Value.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"[CollaborationWarning] Failed to parse payload: {ex.Message}");
            }

            _onWarning(projectId, text);
            return Task.CompletedTask;
        }
    }
}
