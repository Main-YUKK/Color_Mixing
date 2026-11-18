using System.Reflection;
using NUnit.Framework;

// Pembantu untuk PlayMode test: isi field private ([SerializeField]).
// Salinan kecil dari TestHelpers di EditMode, karena assembly EditMode hanya untuk Editor.
public static class PlayTestHelpers
{
    public static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(field, $"Field '{name}' tidak ditemukan di {target.GetType().Name}");
        field.SetValue(target, value);
    }
}
