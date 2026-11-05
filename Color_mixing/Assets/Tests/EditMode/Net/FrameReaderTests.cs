using System;
using NUnit.Framework;

public class FrameReaderTests
{
    private static void Feed(FrameReader reader, byte[] bytes) => reader.Append(bytes, 0, bytes.Length);

    [Test]
    public void SingleFrame_IsReadWithKindAndPayload()
    {
        var reader = new FrameReader();
        Feed(reader, Frame.Build(Frame.Data, new byte[] { 1, 2, 3 }));

        Assert.IsTrue(reader.TryRead(out byte kind, out byte[] payload));
        Assert.AreEqual(Frame.Data, kind);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, payload);
        Assert.IsFalse(reader.TryRead(out _, out _));
    }

    [Test]
    public void EmptyPayload_IsAllowed()
    {
        var reader = new FrameReader();
        Feed(reader, Frame.Build(Frame.Data, new byte[0]));

        Assert.IsTrue(reader.TryRead(out _, out byte[] payload));
        Assert.AreEqual(0, payload.Length);
    }

    [Test]
    public void TwoFramesInOneChunk_AreReadInOrder()
    {
        var reader = new FrameReader();
        var a = Frame.Build(Frame.Welcome, new byte[] { 1 });
        var b = Frame.Build(Frame.Data, new byte[] { 9, 9 });
        var both = new byte[a.Length + b.Length];
        a.CopyTo(both, 0);
        b.CopyTo(both, a.Length);
        Feed(reader, both);

        Assert.IsTrue(reader.TryRead(out byte k1, out byte[] p1));
        Assert.IsTrue(reader.TryRead(out byte k2, out byte[] p2));
        Assert.AreEqual(Frame.Welcome, k1);
        CollectionAssert.AreEqual(new byte[] { 1 }, p1);
        Assert.AreEqual(Frame.Data, k2);
        CollectionAssert.AreEqual(new byte[] { 9, 9 }, p2);
    }

    [Test]
    public void FrameSplitIntoSingleBytes_IsReadOnlyWhenComplete()
    {
        var reader = new FrameReader();
        var frame = Frame.Build(Frame.Data, new byte[] { 5, 6, 7, 8 });

        for (int i = 0; i < frame.Length - 1; i++)
        {
            reader.Append(frame, i, 1);
            Assert.IsFalse(reader.TryRead(out _, out _), $"belum lengkap setelah {i + 1} byte");
        }

        reader.Append(frame, frame.Length - 1, 1);
        Assert.IsTrue(reader.TryRead(out _, out byte[] payload));
        CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, payload);
    }

    [Test]
    public void BytesAfterFrame_StayForTheNextFrame()
    {
        var reader = new FrameReader();
        var first = Frame.Build(Frame.Data, new byte[] { 1 });
        var second = Frame.Build(Frame.Data, new byte[] { 2, 2 });

        Feed(reader, first);
        reader.Append(second, 0, 3);   // sebagian frame kedua
        Assert.IsTrue(reader.TryRead(out _, out _));
        Assert.IsFalse(reader.TryRead(out _, out _));

        reader.Append(second, 3, second.Length - 3);
        Assert.IsTrue(reader.TryRead(out _, out byte[] payload));
        CollectionAssert.AreEqual(new byte[] { 2, 2 }, payload);
    }

    [Test]
    public void LargePayload_GrowsBuffer()
    {
        var reader = new FrameReader();
        var big = new byte[20000];
        for (int i = 0; i < big.Length; i++) big[i] = (byte)(i % 251);
        Feed(reader, Frame.Build(Frame.Data, big));

        Assert.IsTrue(reader.TryRead(out _, out byte[] payload));
        CollectionAssert.AreEqual(big, payload);
    }

    [Test]
    public void ImpossibleLength_ThrowsFormatException()
    {
        var reader = new FrameReader();
        Feed(reader, BitConverter.GetBytes(Frame.MaxLength + 1));

        Assert.Throws<FormatException>(() => reader.TryRead(out _, out _));
    }

    [Test]
    public void ZeroLength_ThrowsFormatException()
    {
        var reader = new FrameReader();
        Feed(reader, BitConverter.GetBytes(0));

        Assert.Throws<FormatException>(() => reader.TryRead(out _, out _));
    }
}
