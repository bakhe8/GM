using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GuaranteeManager.Services
{
    internal static class OperationLogScope
    {
        private sealed class ScopeFrame
        {
            public ScopeFrame(string operationId, string operationName)
            {
                OperationId = operationId;
                OperationName = operationName;
            }

            public string OperationId { get; }

            public string OperationName { get; }
        }

        private sealed class ScopeHandle : IDisposable
        {
            private readonly ScopeFrame _frame;
            private bool _disposed;

            public ScopeHandle(ScopeFrame frame)
            {
                _frame = frame;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Stack<ScopeFrame>? frames = _frames.Value;
                if (frames == null || frames.Count == 0)
                {
                    return;
                }

                if (ReferenceEquals(frames.Peek(), _frame))
                {
                    frames.Pop();
                }

                if (frames.Count == 0)
                {
                    _frames.Value = null;
                }
            }
        }

        private static readonly AsyncLocal<Stack<ScopeFrame>?> _frames = new();

        public static IDisposable Begin(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                operationName = "UnnamedOperation";
            }

            Stack<ScopeFrame> frames = _frames.Value ??= new Stack<ScopeFrame>();
            string operationId = frames.Count == 0
                ? Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
                : frames.Peek().OperationId;

            ScopeFrame frame = new(operationId, operationName);
            frames.Push(frame);
            return new ScopeHandle(frame);
        }

        public static string? CurrentOperationId =>
            _frames.Value is { Count: > 0 } frames
                ? frames.Peek().OperationId
                : null;

        public static string CurrentScopePath =>
            _frames.Value is { Count: > 0 } frames
                ? string.Join(" > ", frames.Reverse().Select(frame => frame.OperationName))
                : string.Empty;
    }
}
