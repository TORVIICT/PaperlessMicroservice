using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Ocr.Infrastructure.Notifications;

namespace Ocr.UnitTests;

public sealed class HttpOcrCompletionNotifierTests
{
    [Fact]
    public async Task NotifyCompletedAsync_RetriesUntilAclConfirms()
    {
        var handler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);
        var notifier = new HttpOcrCompletionNotifier(
            new HttpClient(handler),
            new OcrCompletionNotificationOptions
            {
                Enabled = true,
                CompletionUrl = "http://acl/api/internal/v1/ocr/completed",
                MaxAttempts = 3,
                InitialDelay = TimeSpan.Zero
            },
            NullLogger<HttpOcrCompletionNotifier>.Instance);

        await notifier.NotifyCompletedAsync(Guid.NewGuid(), "correlation");

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task NotifyCompletedAsync_DoesNotCallAclWhenDisabled()
    {
        var handler = new SequenceHandler(HttpStatusCode.OK);
        var notifier = new HttpOcrCompletionNotifier(
            new HttpClient(handler),
            new OcrCompletionNotificationOptions { Enabled = false },
            NullLogger<HttpOcrCompletionNotifier>.Instance);

        await notifier.NotifyCompletedAsync(Guid.NewGuid(), "correlation");

        Assert.Equal(0, handler.CallCount);
    }

    private sealed class SequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
