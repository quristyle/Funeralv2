namespace JSini.Web.Admin.Api;

/// <summary>읽어 가는 바이트를 세어 알려 주는 껍데기. 올리는 쪽은 스트림을 그대로 넘기므로
/// 얼마나 갔는지 알 길이 이것뿐이다 — <c>HttpClient</c> 는 보낸 양을 알려 주지 않는다.
///
/// <para>RAG 문서함과 Gmail 보관함이 함께 쓴다. 셋째가 생기면 <c>Components</c> 로 올린다.</para></summary>
internal sealed class CountingStream(Stream inner, Action<long>? onSent) : Stream
{
    private long _total;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _total;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await inner.ReadAsync(buffer, ct);
        if (n > 0)
        {
            _total += n;
            onSent?.Invoke(_total);
        }

        return n;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        if (n > 0)
        {
            _total += n;
            onSent?.Invoke(_total);
        }

        return n;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
