using System.Net.Http.Json;
using Serilog.Core;
using Serilog.Events;

namespace MFG.Server.Logging;

/// <summary>
/// Error 이상 로그를 Slack Incoming Webhook으로 전송하는 커스텀 Serilog sink.
/// 동일 에러 폭주 시 Slack 채널 스팸을 막기 위해 1분당 최대 N건으로 제한한다.
/// Webhook URL이 비어 있으면 No-op로 동작 (Dev/Test 안전).
/// </summary>
public sealed class SlackWebhookSink : ILogEventSink, IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly string _webhookUrl;
    private readonly int _maxPerMinute;
    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _window = new();

    public SlackWebhookSink(string webhookUrl, int maxPerMinute = 10)
    {
        _webhookUrl = webhookUrl;
        _maxPerMinute = Math.Max(1, maxPerMinute);
    }

    public void Emit(LogEvent logEvent)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl)) return;
        if (logEvent.Level < LogEventLevel.Error) return;

        // Fixed-window rate limit (프로세스 로컬).
        // 단일 인스턴스 전제 — 멀티 인스턴스 확장 시 Redis 기반으로 교체.
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            while (_window.Count > 0 && now - _window.Peek() > TimeSpan.FromMinutes(1))
                _window.Dequeue();

            if (_window.Count >= _maxPerMinute) return;
            _window.Enqueue(now);
        }

        _ = SendAsync(logEvent);
    }

    private async Task SendAsync(LogEvent logEvent)
    {
        try
        {
            var title = logEvent.Level == LogEventLevel.Fatal ? "🔥 FATAL" : "❌ ERROR";
            var source = logEvent.Properties.TryGetValue("SourceContext", out var src)
                ? src.ToString().Trim('"') : "MFG.Server";
            var message = logEvent.RenderMessage();
            var exception = logEvent.Exception?.ToString() ?? string.Empty;

            const int MaxExceptionLen = 1500;
            if (exception.Length > MaxExceptionLen)
                exception = exception[..MaxExceptionLen] + "\n...(truncated)";

            var payload = new
            {
                attachments = new[]
                {
                    new
                    {
                        color = logEvent.Level == LogEventLevel.Fatal ? "danger" : "warning",
                        title = $"{title} [MFG.Server]",
                        text = $"*{source}*\n{message}"
                              + (string.IsNullOrEmpty(exception) ? "" : $"\n```{exception}```"),
                        ts = logEvent.Timestamp.ToUnixTimeSeconds()
                    }
                }
            };

            using var resp = await Http.PostAsJsonAsync(_webhookUrl, payload);
            // 실패해도 다시 로그하지 않는다 — 자기 참조 루프 방지.
        }
        catch
        {
            // 알림 전송 실패는 삼킨다 — 로깅 자체가 실패 원인이 되면 안 됨.
        }
    }

    public void Dispose()
    {
        // HttpClient는 static 공유이므로 여기서 해제하지 않는다.
    }
}
