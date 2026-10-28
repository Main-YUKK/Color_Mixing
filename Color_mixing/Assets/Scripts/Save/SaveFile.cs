using System;

// Satu file save = satu room. Disimpan sebagai JSON di device host.
[Serializable]
public class SaveFile
{
    public string id;
    public string roomName;
    public string roomCode;
    public string hostName;
    public int completedDay;    // hari terakhir yang selesai (0 = belum ada)
    public int nextDay = 1;     // hari awal saat dilanjutkan
}
