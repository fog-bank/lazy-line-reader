using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace LazyLineReader;

[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix")]
public class ObservableDeque<T>(int maxCount) : ICollection<T>, INotifyPropertyChanged, INotifyCollectionChanged
{
    private T[] items = new T[maxCount];
    private int offset;

    public T this[int index] => items[(index + offset) % items.Length];

    public int Count { get; private set; }

    public bool IsReadOnly => false;

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return this[i];
    }

    public void AddLast(T item)
    {
        var removed = items[(Count + offset) % items.Length];
        items[(Count + offset) % items.Length] = item;

        if (Count == items.Length)
        {
            offset++;
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, removed, 0);
        }
        else
        {
            Count++;
            OnPropertyChanged(nameof(Count));
        }
        OnPropertyChanged("Item[]");
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, Count - 1);
    }

    public void AddFirst(T item)
    {
        var removed = items[(items.Length - 1 + offset) % items.Length];
        items[(items.Length - 1 + offset) % items.Length] = item;

        if (Count == items.Length)
        {
            offset--;
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, removed, Count - 1);
        }
        else
        {
            Count++;
            OnPropertyChanged(nameof(Count));
        }
        OnPropertyChanged("Item[]");
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, 0);
    }

    public void Clear()
    {
        Array.Clear(items, 0, items.Length);
        offset = 0;

        if (Count > 0)
        {
            Count = 0;

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(NotifyCollectionChangedAction.Reset);
        }
    }

    public ObservableDeque<T> Clone()
    {
        var newobj = new ObservableDeque<T>(items.Length);

        for (int i = 0; i < Count; i++)
            newobj.items[i] = this[i];

        newobj.Count = Count;
        return newobj;
    }

    public void CopyFrom(ObservableDeque<T> src)
    {
        items = src.items;
        offset = src.offset;
        Count = src.Count;

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged("Item[]");
        OnCollectionChanged(NotifyCollectionChangedAction.Reset);
    }

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void OnCollectionChanged(NotifyCollectionChangedAction action)
        => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action));

    private void OnCollectionChanged(NotifyCollectionChangedAction action, T item, int index)
        => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, item, index));

    #region Implicit Interface Implementations

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void ICollection<T>.CopyTo(T[] array, int arrayIndex)
    {
        foreach (var item in this)
            array[arrayIndex++] = item;
    }

    bool ICollection<T>.Contains(T item)
    {
        foreach (var thisItem in this)
        {
            if (EqualityComparer<T>.Default.Equals(item, thisItem))
                return true;
        }
        return false;
    }

    void ICollection<T>.Add(T item) => AddLast(item);

    bool ICollection<T>.Remove(T item) => false;

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
}
