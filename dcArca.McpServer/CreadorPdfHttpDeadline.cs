namespace dcArca.McpServer;

internal sealed class CreadorPdfHttpDeadline : IDisposable
{
    private readonly CancellationTokenSource? _timeoutSource;

    private CreadorPdfHttpDeadline(TimeSpan timeout, CancellationToken callerToken)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            Token = callerToken;
            return;
        }

        _timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        _timeoutSource.CancelAfter(timeout);
        Token = _timeoutSource.Token;
    }

    public CancellationToken Token { get; }

    public static CreadorPdfHttpDeadline Start(TimeSpan timeout, CancellationToken callerToken)
        => new(timeout, callerToken);

    public void Dispose() => _timeoutSource?.Dispose();
}
