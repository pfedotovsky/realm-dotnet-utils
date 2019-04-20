using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Realms;

namespace RealmClone
{
    public static class RealmExtensions
    {
        public static T Clone<T>(this T source) where T : RealmObject
        {
            // If source is null return null
            if (source == null)
            {
                return default;
            }

            var target = Activator.CreateInstance<T>();
            var targetType = typeof(T);

            // List of skip namespaces
            var skipNamespaces = new List<string>
            {
                typeof(Realm).Namespace
            };

            // Get the Namespace name of Generic Collection
            var collectionNamespace = typeof(List<string>).Namespace;

            // Flags to get properties
            var flags = BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;

            // Get target properties list which follows the flags
            var targetProperties = targetType.GetProperties(flags)
                .Where(x => !x.IsDefined(typeof(IgnoredAttribute), true))
                .Where(x => !x.IsDefined(typeof(BacklinkAttribute), true));

            // Enumerate properties
            foreach (var property in targetProperties)
            {
                // Skip property if it's belongs to namespace available in skipNamespaces list
                if (skipNamespaces.Contains(property.DeclaringType.Namespace))
                    continue;

                // Get property information and check if we can write value in it
                var propertyInfo = targetType.GetProperty(property.Name, flags);
                if (propertyInfo == null)
                {
                    continue;
                }

                // Get value from the source
                var sourceValue = property.GetValue(source);

                // If property derived from the RealmObject then Clone that too
                if (property.PropertyType.IsSubclassOf(typeof(RealmObject)) && sourceValue is RealmObject)
                {
                    var propertyType = property.PropertyType;
                    var convertedSourceValue = Convert.ChangeType(sourceValue, propertyType);
                    sourceValue = typeof(RealmExtensions).GetMethod("Clone", BindingFlags.Static | BindingFlags.Public)
                            .MakeGenericMethod(propertyType).Invoke(convertedSourceValue, new[] { convertedSourceValue });
                }

                // Check if property belongs to the collection namespace and original value is not null
                if (property.PropertyType.Namespace == collectionNamespace && sourceValue != null)
                {
                    var sourceList = sourceValue as IEnumerable;

                    var targetList = property.GetValue(target) as IList;

                    // Enumerate source list and recursively call Clone method on each object
                    foreach (var item in sourceList)
                    {
                        var value = typeof(RealmExtensions).GetMethod("Clone", BindingFlags.Static | BindingFlags.Public)
                            .MakeGenericMethod(item.GetType()).Invoke(item, new[] { item });
                        targetList.Add(value);
                    }
                }
                else
                {
                    propertyInfo.SetValue(target, sourceValue);
                }
            }

            return target;
        }
    }
}