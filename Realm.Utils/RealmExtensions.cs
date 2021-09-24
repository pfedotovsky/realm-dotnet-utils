using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Realms.Utils
{
        public static class RealmExtensions
    {
        private const BindingFlags Flags = BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;
        private static readonly string? CollectionNamespace = typeof(List<string>).Namespace;

        private static readonly string[] SkipNamespaces =
        {
            typeof(Realm).Namespace
        };

        public static T Clone<T>(this T obj)
            where T : RealmObject
        {
            return (T)CloneInternal(obj)!;
        }

        public static IList<T> ToClonedList<T>(this IEnumerable<T> enumerable)
            where T : RealmObject
        {
            return enumerable.ToClonedEnumerable().ToList();
        }

        public static IEnumerable<T> ToClonedEnumerable<T>(this IEnumerable<T> enumerable)
            where T : RealmObject
        {
            return enumerable.Select(item => item.Clone()).ToList();
        }

        public static RealmObject? Clone(this RealmObject? source)
        {
            return (RealmObject?)CloneInternal(source);
        }

        public static EmbeddedObject? Clone(this EmbeddedObject? source)
        {
            return (EmbeddedObject?)CloneInternal(source);
        }

        private static RealmObjectBase? CloneInternal(this RealmObjectBase? source)
        {
            if (source == null)
            {
                return default;
            }

            Type targetType = source.GetType();
            object? target = Activator.CreateInstance(targetType);

            IEnumerable<PropertyInfo> targetProperties = targetType.GetProperties(Flags)
                                                                   .Where(x => !x.IsDefined(typeof(IgnoredAttribute)) && !x.IsDefined(typeof(BacklinkAttribute), true))
                                                                   .ToArray();

            foreach (var property in targetProperties)
            {
                //underlying method supports null
                if (SkipNamespaces.Contains(property.DeclaringType?.Namespace!))
                {
                    continue;
                }

                PropertyInfo? propertyInfo = targetType.GetProperty(property.Name, Flags);
                if (propertyInfo == null)
                {
                    continue;
                }

                object? sourceValue = property.GetValue(source);
                switch (sourceValue)
                {
                    case RealmObject realmObject:
                        RealmObject? clonedRealmObject = realmObject.Clone();
                        propertyInfo.SetValue(target, clonedRealmObject);
                        continue;
                    case EmbeddedObject embeddedObject:
                        EmbeddedObject? clonedEmbeddedObject = embeddedObject.Clone();
                        propertyInfo.SetValue(target, clonedEmbeddedObject);
                        continue;
                }

                if (property.PropertyType.Namespace == CollectionNamespace && sourceValue != null)
                {
                    var sourceList = sourceValue as IEnumerable;
                    var targetList = property.GetValue(target) as IList;
                    if (sourceList != null && targetList != null)
                    {
                        // Enumerate source list and recursively call Clone method on each object
                        foreach (var item in sourceList)
                        {
                            object? value = item switch
                            {
                                null => null,
                                ValueType => item,
                                string => item,
                                RealmObject realmObject => realmObject.Clone(),
                                EmbeddedObject embeddedObject => embeddedObject.Clone(),
                                _ => throw new ArgumentOutOfRangeException(nameof(item), item.GetType(), "Can only clone value types, strings, RealmObject and EmbeddedObject")
                            };

                            targetList.Add(value);
                        }
                    }

                    continue;
                }

                propertyInfo.SetValue(target, sourceValue);
            }

            return (RealmObjectBase)target;
        }
    }
}
