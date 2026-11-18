using System;
using System.Text;
using NUnit.Framework;

public class NetMessageTests
{
    [Serializable]
    private struct StartData { public int day; public string room; }

    [Test]
    public void EncodeDecode_KeepsTypeAndPayload()
    {
        var original = NetMessage.Create(NetMessageType.Start, new StartData { day = 4, room = "Room Ivone" });

        Assert.IsTrue(NetMessage.TryDecode(original.Encode(), out var decoded));

        Assert.AreEqual(NetMessageType.Start, decoded.type);
        var data = decoded.Read<StartData>();
        Assert.AreEqual(4, data.day);
        Assert.AreEqual("Room Ivone", data.room);
    }

    [Test]
    public void Create_WithoutData_HasNoPayload()
    {
        var msg = NetMessage.Create(NetMessageType.Ping);

        Assert.IsTrue(NetMessage.TryDecode(msg.Encode(), out var decoded));
        Assert.AreEqual(NetMessageType.Ping, decoded.type);
        Assert.IsTrue(string.IsNullOrEmpty(decoded.payload));
        Assert.AreEqual(0, decoded.Read<StartData>().day);   // tanpa payload: nilai default, bukan error
    }

    [Test]
    public void TryDecode_Garbage_ReturnsFalse()
    {
        Assert.IsFalse(NetMessage.TryDecode(Encoding.UTF8.GetBytes("bukan json {{{"), out var msg));
        Assert.IsNull(msg);
    }

    [Test]
    public void TryDecode_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(NetMessage.TryDecode(null, out _));
        Assert.IsFalse(NetMessage.TryDecode(new byte[0], out _));
    }

    [Test]
    public void TryDecode_JsonWithoutType_ReturnsFalse()
    {
        // JSON valid tapi bukan pesan game (mis. pesan lobby tanpa field type kita).
        Assert.IsFalse(NetMessage.TryDecode(Encoding.UTF8.GetBytes("{\"payload\":\"x\"}"), out _));
    }
}
