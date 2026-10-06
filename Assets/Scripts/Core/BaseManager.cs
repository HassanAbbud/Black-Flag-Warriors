using System;
using System.Reflection;
using UnityEngine;

public abstract class BaseManager<T>
{
    private static T instance;
    
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                Type type = typeof(T);
                ConstructorInfo info = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (info == null)
                {
                    throw new Exception($"A private or protected constructor is missing for '{type.Name}'.");
                }
                instance = (T)info.Invoke(null);
            }
            return instance;
        }
    }

}
