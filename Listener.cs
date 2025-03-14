public class Listener
{
    List<Action> listeners = new List<Action>();

    public void Invoke()
    {
        foreach (var listener in listeners)
        {
            listener.Invoke();
        }
    }

    public void AddListener(Action action)
    {
        listeners.Add(action);
    }
}

public class Listener<T>
{
    List<Action<T>> listeners = new List<Action<T>>();

    public void Invoke(T data)
    {
        foreach (var listener in listeners)
        {
            listener.Invoke(data);
        }
    }

    public void AddListener(Action<T> action)
    {
        listeners.Add(action);
    }
}
