using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Pembantu untuk test: isi field private ([SerializeField]) and create dummy data
public static class TestHelpers
{
    public static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(field, $"Field '{name}' tidak ditemukan di {target.GetType().Name}");
        field.SetValue(target, value);
    }

    public static void Invoke(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(m, $"Method '{method}' tidak ditemukan di {target.GetType().Name}");
        m.Invoke(target, null);
    }

    public static ClientRequest Request(string id, RequestDifficulty difficulty, string description = "")
    {
        return new ClientRequest { requestId = id, requestName = id, description = description, difficulty = difficulty };
    }

    public static ClientRequestDatabase Database(params ClientRequest[] requests)
    {
        var db = ScriptableObject.CreateInstance<ClientRequestDatabase>();
        SetField(db, "clientRequests", new List<ClientRequest>(requests));
        return db;
    }

    // days[i] = daftar level klien untuk hari ke-(i+1).
    public static DaySchedule Schedule(int loopStartIndex, params RequestDifficulty[][] days)
    {
        var schedule = ScriptableObject.CreateInstance<DaySchedule>();
        var list = new List<DaySchedule.Day>();
        foreach (var clients in days)
            list.Add(new DaySchedule.Day { clients = new List<RequestDifficulty>(clients) });
        SetField(schedule, "days", list);
        SetField(schedule, "loopStartIndex", loopStartIndex);
        return schedule;
    }
}
