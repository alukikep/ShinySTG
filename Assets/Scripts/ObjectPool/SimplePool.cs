using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用的泛型对象池类
/// </summary>
public class ObjectPool<T> where T : Component
{
    private readonly Queue<T> availableObjects = new Queue<T>();
    private readonly T prefab;
    private readonly Transform parent;
    private readonly int initialSize;

    public ObjectPool(T prefab, Transform parent, int initialSize = 10)
    {
        this.prefab = prefab;
        this.parent = parent;
        this.initialSize = initialSize;

        // 预加载对象
        Preload(initialSize);
    }

    /// <summary>
    /// 预加载指定数量的对象
    /// </summary>
    public void Preload(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T obj = CreateNewObject();
            obj.gameObject.SetActive(false);
            availableObjects.Enqueue(obj);
        }
    }

    /// <summary>
    /// 创建新对象
    /// </summary>
    private T CreateNewObject()
    {
        T obj = Object.Instantiate(prefab, parent);
        obj.gameObject.SetActive(false);
        return obj;
    }

    /// <summary>
    /// 从对象池获取对象
    /// </summary>
    public T Get(Vector3 position, Quaternion rotation)
    {
        T obj;

        if (availableObjects.Count > 0)
        {
            obj = availableObjects.Dequeue();
        }
        else
        {
            // 如果池为空，创建新对象
            obj = CreateNewObject();
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.gameObject.SetActive(true);

        // 如果实现了IPoolable接口，调用OnSpawn
        if (obj is IPoolable poolable)
        {
            poolable.OnSpawn();
        }

        return obj;
    }

    /// <summary>
    /// 简化版获取（使用原始旋转）
    /// </summary>
    public T Get(Vector3 position)
    {
        return Get(position, Quaternion.identity);
    }

    /// <summary>
    /// 简化版获取（使用预设位置和旋转）
    /// </summary>
    public T Get()
    {
        return Get(Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// 归还对象到对象池
    /// </summary>
    public void Return(T obj)
    {
        // 如果实现了IPoolable接口，调用OnDespawn
        if (obj is IPoolable poolable)
        {
            poolable.OnDespawn();
        }

        obj.gameObject.SetActive(false);
        availableObjects.Enqueue(obj);
    }

    /// <summary>
    /// 获取池中可用对象数量
    /// </summary>
    public int AvailableCount => availableObjects.Count;
}

/// <summary>
/// 对象池项接口，用于需要在对象取出/归还时执行特定逻辑
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 对象从池中取出时调用
    /// </summary>
    void OnSpawn();

    /// <summary>
    /// 对象归还到池中时调用
    /// </summary>
    void OnDespawn();
}

/// <summary>
/// 全局对象池管理器
/// 使用方法：
/// 1. 自动模式：直接调用 Pool.Spawn(prefab, position, rotation) 即可，无需手动管理池
/// 2. 手动模式：调用 ObjectPoolManager.Instance.GetPool<T>(prefab, parent, initialSize).Get(position, rotation)
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    private Dictionary<System.Type, object> pools = new Dictionary<System.Type, System.Object>();
    private Dictionary<int, object> poolsByInstanceID = new Dictionary<int, object>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 获取或创建指定类型的对象池
    /// </summary>
    public ObjectPool<T> GetPool<T>(T prefab, Transform parent = null, int initialSize = 10) where T : Component
    {
        System.Type type = typeof(T);

        if (!pools.ContainsKey(type))
        {
            pools[type] = new ObjectPool<T>(prefab, parent ?? transform, initialSize);
        }

        return (ObjectPool<T>)pools[type];
    }

    /// <summary>
    /// 根据预制体实例ID获取或创建对象池
    /// </summary>
    public ObjectPool<T> GetPoolByPrefab<T>(T prefab, Transform parent = null, int initialSize = 10) where T : Component
    {
        int instanceID = prefab.GetInstanceID();

        if (!poolsByInstanceID.ContainsKey(instanceID))
        {
            poolsByInstanceID[instanceID] = new ObjectPool<T>(prefab, parent ?? transform, initialSize);
        }

        return (ObjectPool<T>)poolsByInstanceID[instanceID];
    }

    /// <summary>
    /// 归还对象到对应的对象池
    /// </summary>
    public void Return<T>(T obj) where T : Component
    {
        System.Type type = typeof(T);

        if (pools.ContainsKey(type))
        {
            ((ObjectPool<T>)pools[type]).Return(obj);
        }
        else
        {
            // 如果找不到对应的池，销毁对象
            Debug.LogWarning($"No pool found for type {type.Name}, destroying object");
            Destroy(obj.gameObject);
        }
    }

    /// <summary>
    /// 清除所有对象池
    /// </summary>
    public void ClearAllPools()
    {
        pools.Clear();
        poolsByInstanceID.Clear();
    }
}

/// <summary>
/// 简化版的全局访问类，提供更便捷的API
/// 使用方法：直接调用 Pool.Spawn(prefab, position, rotation)
/// </summary>
public static class Pool
{
    private static Transform poolParent;
    private static Dictionary<int, Queue<GameObject>> gameObjectPools = new Dictionary<int, Queue<GameObject>>();

    /// <summary>
    /// 获取或创建对象池父物体
    /// </summary>
    private static Transform PoolParent
    {
        get
        {
            if (poolParent == null)
            {
                GameObject go = new GameObject("Pool");
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(go);
                }
                poolParent = go.transform;
            }
            return poolParent;
        }
    }

    /// <summary>
    /// 生成对象（自动管理对象池）- GameObject版本
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="position">生成位置</param>
    /// <param name="rotation">生成旋转</param>
    /// <param name="initialSize">初始池大小（仅首次创建时生效）</param>
    /// <returns>生成的GameObject</returns>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, int initialSize = 10)
    {
        if (prefab == null)
        {
            Debug.LogError("Pool.Spawn: prefab is null!");
            return null;
        }

        int instanceID = prefab.GetInstanceID();

        // 如果池不存在，创建新池并预加载
        if (!gameObjectPools.ContainsKey(instanceID))
        {
            gameObjectPools[instanceID] = new Queue<GameObject>();
            PreloadGameObjects(prefab, instanceID, initialSize);
        }

        GameObject obj;

        // 从池中取出或创建新对象
        if (gameObjectPools[instanceID].Count > 0)
        {
            obj = gameObjectPools[instanceID].Dequeue();
        }
        else
        {
            obj = Object.Instantiate(prefab, PoolParent);
            obj.SetActive(false);
            
            // 添加PoolObject组件来存储prefab的instanceID
            PoolObject poolObj = obj.AddComponent<PoolObject>();
            poolObj.PrefabInstanceID = instanceID;
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        // 触发IPoolable接口
        foreach (Component component in obj.GetComponents<Component>())
        {
            if (component is IPoolable poolable)
            {
                poolable.OnSpawn();
            }
        }

        return obj;
    }

    /// <summary>
    /// 生成对象（使用默认旋转）- GameObject版本
    /// </summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, int initialSize = 10)
    {
        return Spawn(prefab, position, Quaternion.identity, initialSize);
    }

    /// <summary>
    /// 生成对象（使用预设位置）- GameObject版本
    /// </summary>
    public static GameObject Spawn(GameObject prefab, int initialSize = 10)
    {
        return Spawn(prefab, Vector3.zero, Quaternion.identity, initialSize);
    }

    /// <summary>
    /// 归还GameObject到对象池
    /// </summary>
    public static void Despawn(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        // 尝试获取存储的prefab instanceID
        PoolObject poolObject = obj.GetComponent<PoolObject>();
        int instanceID = poolObject != null ? poolObject.PrefabInstanceID : obj.GetInstanceID();

        if (!gameObjectPools.ContainsKey(instanceID))
        {
            // 如果找不到对应的池，直接销毁
            Debug.LogWarning($"No pool found for {obj.name}, destroying object");
            Object.Destroy(obj);
            return;
        }

        // 触发IPoolable接口
        foreach (Component component in obj.GetComponents<Component>())
        {
            if (component is IPoolable poolable)
            {
                poolable.OnDespawn();
            }
        }

        obj.SetActive(false);
        gameObjectPools[instanceID].Enqueue(obj);
    }

    /// <summary>
    /// 预加载GameObject到池中
    /// </summary>
    private static void PreloadGameObjects(GameObject prefab, int instanceID, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Object.Instantiate(prefab, PoolParent);
            obj.SetActive(false);

            // 添加PoolObject组件来存储prefab的instanceID
            PoolObject poolObj = obj.AddComponent<PoolObject>();
            poolObj.PrefabInstanceID = instanceID;

            gameObjectPools[instanceID].Enqueue(obj);
        }
    }

    /// <summary>
    /// 生成对象（自动管理对象池）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="prefab">预制体</param>
    /// <param name="position">生成位置</param>
    /// <param name="rotation">生成旋转</param>
    /// <param name="initialSize">初始池大小（仅首次创建时生效）</param>
    /// <returns>生成的对象</returns>
    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, int initialSize = 10) where T : Component
    {
        if (prefab == null)
        {
            Debug.LogError("Pool.Spawn: prefab is null!");
            return null;
        }

        EnsureManagerExists();
        return ObjectPoolManager.Instance.GetPoolByPrefab(prefab, PoolParent, initialSize).Get(position, rotation);
    }

    /// <summary>
    /// 生成对象（使用默认旋转）
    /// </summary>
    public static T Spawn<T>(T prefab, Vector3 position, int initialSize = 10) where T : Component
    {
        return Spawn(prefab, position, Quaternion.identity, initialSize);
    }

    /// <summary>
    /// 生成对象（使用预设位置）
    /// </summary>
    public static T Spawn<T>(T prefab, int initialSize = 10) where T : Component
    {
        return Spawn(prefab, Vector3.zero, Quaternion.identity, initialSize);
    }

    /// <summary>
    /// 归还对象到对象池
    /// </summary>
    public static void Despawn<T>(T obj) where T : Component
    {
        if (obj == null)
        {
            return;
        }

        EnsureManagerExists();
        ObjectPoolManager.Instance.Return(obj);
    }

    /// <summary>
    /// 确保对象池管理器存在
    /// </summary>
    private static void EnsureManagerExists()
    {
        if (ObjectPoolManager.Instance == null)
        {
            GameObject go = new GameObject("ObjectPoolManager");
            go.AddComponent<ObjectPoolManager>();
        }
    }
}

/// <summary>
/// 扩展方法，方便使用对象池
/// </summary>
public static class ObjectPoolExtensions
{
    /// <summary>
    /// 归还对象到对象池（扩展方法）
    /// 使用方式：bullet.ReturnToPool();
    /// </summary>
    public static void ReturnToPool<T>(this T obj) where T : Component
    {
        Pool.Despawn(obj);
    }
}

/// <summary>
/// 对象基类扩展，提供便捷的池化方法
/// 使用方式：让需要对象池的类继承此类，或使用扩展方法
/// </summary>
public abstract class PoolableMonoBehaviour : MonoBehaviour, IPoolable
{
    /// <summary>
    /// 对象从池中取出时调用
    /// </summary>
    public virtual void OnSpawn() { }

    /// <summary>
    /// 对象归还到池中时调用
    /// </summary>
    public virtual void OnDespawn() { }

    /// <summary>
    /// 便捷方法：归还到对象池
    /// </summary>
    public void ReturnToPool()
    {
        Pool.Despawn(this);
    }
}

/// <summary>
/// 对象池辅助组件，用于存储预制体实例ID
/// 当对象被Spawn时自动添加此组件，用于Despawn时能找到正确的池
/// </summary>
public class PoolObject : MonoBehaviour
{
    /// <summary>
    /// 对应预制体的InstanceID
    /// </summary>
    public int PrefabInstanceID { get; set; }
}
