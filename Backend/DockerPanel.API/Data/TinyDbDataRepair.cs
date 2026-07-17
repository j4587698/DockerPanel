using System.Reflection;
using DockerPanel.API.Models;
using Serilog;
using TinyDb;
using TinyDb.Bson;
using TinyDb.Core;
using TinyDb.Serialization;

namespace DockerPanel.API.Data;

/// <summary>
/// TinyDb 数据兼容修复。
/// </summary>
internal static class TinyDbDataRepair
{
    private const string DomainMappingsCollection = "domain_mappings";
    private static readonly BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

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
            var documents = ReadRawDocuments(dbPath, DomainMappingsCollection);
            if (documents.Count == 0)
            {
                return;
            }

            var duplicateGroups = documents
                .Select((document, index) => new
                {
                    Document = document,
                    Index = index,
                    Mapping = BsonMapper.ToObject<DomainMapping>(document)!
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Mapping.Id))
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
                DeleteAllRawDocumentsById(dbPath, DomainMappingsCollection, group.Key, group.Count());
            }

            using var database = new TinyDbEngine(dbPath);
            var collection = database.GetCollection<DomainMapping>(DomainMappingsCollection);
            foreach (var mapping in mappingsToKeep)
            {
                collection.Insert(mapping);
            }

            database.Flush();
            Log.Warning("已修复 TinyDb 域名映射重复主键: {Count} 组", duplicateGroups.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TinyDb 域名映射重复主键修复失败，将继续按原流程启动");
        }
    }

    private static List<BsonDocument> ReadRawDocuments(string dbPath, string collectionName)
    {
        using var database = new TinyDbEngine(dbPath);
        if (!database.CollectionExists(collectionName))
        {
            return new List<BsonDocument>();
        }

        var engineType = typeof(TinyDbEngine);
        var getCollectionState = engineType.GetMethod("GetCollectionState", InstanceNonPublic)
            ?? throw new MissingMethodException(nameof(TinyDbEngine), "GetCollectionState");
        var readAllDocumentsSnapshot = engineType.GetMethod("ReadAllDocumentsSnapshot", InstanceNonPublic)
            ?? throw new MissingMethodException(nameof(TinyDbEngine), "ReadAllDocumentsSnapshot");

        var state = getCollectionState.Invoke(database, new object[] { collectionName });
        var documents = readAllDocumentsSnapshot.Invoke(database, new object?[] { collectionName, state, CancellationToken.None });

        return ((IEnumerable<BsonDocument>)documents!).ToList();
    }

    private static void DeleteAllRawDocumentsById(string dbPath, string collectionName, string id, int expectedCount)
    {
        var maxAttempts = Math.Max(expectedCount + 2, 4);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!RawDocumentIdExists(dbPath, collectionName, id))
            {
                return;
            }

            using var database = new TinyDbEngine(dbPath);
            var deleteDocument = typeof(TinyDbEngine).GetMethod(
                "DeleteDocument",
                InstanceNonPublic | BindingFlags.Public)
                ?? throw new MissingMethodException(nameof(TinyDbEngine), "DeleteDocument");

            var deleted = (int)deleteDocument.Invoke(database, new object?[] { collectionName, (BsonValue)id })!;
            database.Flush();

            if (deleted == 0)
            {
                return;
            }
        }
    }

    private static bool RawDocumentIdExists(string dbPath, string collectionName, string id)
    {
        return ReadRawDocuments(dbPath, collectionName)
            .Select(document => BsonMapper.ToObject<DomainMapping>(document)!.Id)
            .Any(existingId => string.Equals(existingId, id, StringComparison.Ordinal));
    }
}
