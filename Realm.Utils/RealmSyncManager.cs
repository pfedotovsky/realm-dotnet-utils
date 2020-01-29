using System;
using System.Collections.Generic;
using System.Linq;
using Realms.Schema;

namespace Realms.Utils
{
    public static class RealmSyncManager
    {
        public static void UpdateRealm(Realm fromRealm, Realm toRealm)
        {
            // block update if incoming realm is empty
            if (!fromRealm.All(toRealm.Schema.First().Name).Any())
            {
                throw new InvalidOperationException("Attempt to wipe data from realm");
            }

            var adjucencyList = GetAdjecencyList(toRealm.Schema);
            var updateOrder = BreadthFirstSearch(adjucencyList).Reverse();

            using (var transaction = toRealm.BeginWrite())
            {
                foreach (var objectSchema in updateOrder)
                {
                    var keyProperty = objectSchema.Single(property => property.IsPrimaryKey);
                    MoveRealmObjects(fromRealm: fromRealm, toRealm: toRealm, objectSchema.Name, keyProperty.Name);
                }

                transaction.Commit();
            }
        }

        private static Dictionary<ObjectSchema, HashSet<ObjectSchema>> GetAdjecencyList(RealmSchema realmSchema)
        {
            var resultDictionary = new Dictionary<ObjectSchema, HashSet<ObjectSchema>>();

            var type = typeof(ObjectSchema);
            var fieldType = type.GetField("Type", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var objectSchema in realmSchema)
            {
                var hashset = new HashSet<ObjectSchema>();

                var objectType = fieldType.GetValue(objectSchema) as Type;

                var backlinks = objectType.GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(BacklinkAttribute)));

                foreach (var property in objectSchema)
                {
                    if (backlinks.Any(backlink => backlink.Name == property.Name))
                    {
                        continue;
                    }

                    var existingObjectSchema = realmSchema.SingleOrDefault(obj => obj.Name == property.ObjectType);
                    if (existingObjectSchema != null)
                    {
                        hashset.Add(existingObjectSchema);
                    }
                }

                resultDictionary[objectSchema] = hashset;
            }

            return resultDictionary;
        }

        private static HashSet<T> BreadthFirstSearch<T>(Dictionary<T, HashSet<T>> adjacencyList)
        {
            var visited = new HashSet<T>();

            foreach (var currentVertex in adjacencyList)
            {
                if (visited.Contains(currentVertex.Key))
                    continue;

                var queue = new Queue<T>();
                queue.Enqueue(currentVertex.Key);

                while (queue.Count > 0)
                {
                    var vertex = queue.Dequeue();

                    if (visited.Contains(vertex))
                        continue;

                    visited.Add(vertex);

                    foreach (var neighbor in adjacencyList[vertex])
                        if (!visited.Contains(neighbor))
                            queue.Enqueue(neighbor);
                }
            }

            return visited;
        }

        private static void MoveRealmObjects(Realm fromRealm, Realm toRealm, string className, string primaryKeyPropertyName)
        {
            var newObjects = fromRealm.All(className)
                .ToList()
                .Cast<RealmObject>()
                .Select(RealmExtensions.Clone)
                .ToList();

            var oldObjects = toRealm.All(className)
                .ToList()
                .Cast<RealmObject>()
                .ToList();

            var objectsToRemove = oldObjects.Where(obj =>
            {
                var type = obj.GetType();

                var primaryKeyProperty = type.GetProperty(primaryKeyPropertyName);

                var sourcePropertyValue = primaryKeyProperty.GetValue(obj);

                return !newObjects.Any(newObject => primaryKeyProperty.GetValue(newObject).GetHashCode() == sourcePropertyValue.GetHashCode());
            });

            foreach (var @object in objectsToRemove)
            {
                toRealm.Remove(@object);
            }
            foreach (var @object in newObjects)
            {
                toRealm.Add(@object, update: true);
            }
        }
    }
}
