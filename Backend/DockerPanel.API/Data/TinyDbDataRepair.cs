using DockerPanel.API.Models;
using Serilog;
using TinyDb;
using TinyDb.Bson;
using TinyDb.Core;

namespace DockerPanel.API.Data;

/// <summary>
/// TinyDb 数据兼容修复。
/// </summary>
internal static class TinyDbDataRepair
{
    private const string DomainMappingsCollection = "domain_mappings";

    public static void Repair(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            return;
        }

        RepairDuplicateDomainMappingIds(dbPath);
    }

    private static void RepairDuplicateDomainMappingIds(string dbPath)
    {
        try
        {
            using var database = new TinyDbEngine(dbPath);
            var collection = database.GetCollection<DomainMapping>(DomainMappingsCollection);

            var mappings = collection.FindAll().ToList();
            if (mappings.Count == 0)
            {
                return;
            }

            var indexed = mappings
                .Select((mapping, index) => new { Mapping = mapping, Index = index })
                .Where(item => !string.IsNullOrWhiteSpace(item.Mapping.Id))
                .ToList();

            var duplicateGroups = indexed
                .GroupBy(item => item.Mapping.Id)
                .Where(group => group.Count() > 1)
                .ToList();

            if (duplicateGroups.Count == 0)
            {
                return;
            }

            var mappingsToKeep = duplicateGroups
                .Select(group => group
                    .OrderByDescending(item => item.Mapping.UpdatedAt)
                    .ThenByDescending(item => item.Index)
                    .First()
                    .Mapping)
                .ToList();

            foreach (var group in duplicateGroups)
            {
                foreach (var item in group.Where(item => !mappingsToKeep.Contains(item.Mapping)).ToList())
                {
                    collection.Delete(item.Mapping.Id);
                }
            }

            foreach (var mapping in mappingsToKeep)
            {
                if (collection.FindById(mapping.Id) == null)
                {
                    collection.Insert(mapping);
                }
            }

            database.Flush();
            Log.Warning("已修复 TinyDb 域名映射重复主键: {Count} 组", duplicateGroups.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TinyDb 域名映射重复主键修复失败，将继续按原流程启动");
        }
    }
}