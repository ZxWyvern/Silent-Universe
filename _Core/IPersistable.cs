/// <summary>
/// IPersistable — interface untuk sistem yang perlu flush data ke SaveFile
/// sebelum save ditulis ke disk.
///
/// Fase 2: Menggantikan delegate RegisterPersistCallback di GameSave.
/// Implementors: PlayerInventory, PlayerDiskInventory.
/// GameSaveService (injectable) menerima IReadOnlyList<IPersistable> via inject
/// dan memanggil Persist() pada semua implementor sebelum ForceWrite().
///
/// Selama Fase 2-3, GameSave static masih ada sebagai compatibility shim.
/// Fase 4 akan hapus RegisterPersistCallback sepenuhnya.
/// </summary>
public interface IPersistable
{
    void Persist();
}