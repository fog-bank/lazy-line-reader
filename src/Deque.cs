using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LazyLineReader;

public class Deque<T> : ICollection<T>, INotifyPropertyChanged, INotifyCollectionChanged
{
	private T[] items;
	private int offset;
	private int count;

	public Deque(int maxCount)
	{
		items = new T[maxCount];
	}

    public T this[int index] => items[(index + offset) % items.Length];

    public int Count => count;

    public bool IsReadOnly => false;

    public IEnumerator<T> GetEnumerator()
	{
		for (int i = 0; i < count; i++)
			yield return this[i];
	}

	public void AddLast(T item)
	{
		var removed = items[(count + offset) % items.Length];
		items[(count + offset) % items.Length] = item;

		if (count == items.Length)
		{
			offset++;
			OnCollectionChanged(NotifyCollectionChangedAction.Remove, removed, 0);
		}
		else
		{
			count++;
			OnPropertyChanged(nameof(Count));
		}
		OnPropertyChanged("Item[]");
		OnCollectionChanged(NotifyCollectionChangedAction.Add, item, count - 1);
	}

	public void AddFirst(T item)
	{
		var removed = items[(items.Length - 1 + offset) % items.Length];
		items[(items.Length - 1 + offset) % items.Length] = item;

		if (count == items.Length)
		{
			offset--;
			OnCollectionChanged(NotifyCollectionChangedAction.Remove, removed, count - 1);
		}
		else
		{
			count++;
			OnPropertyChanged(nameof(Count));
		}
		OnPropertyChanged("Item[]");
		OnCollectionChanged(NotifyCollectionChangedAction.Add, item, 0);
	}

	public void Clear()
	{
		Array.Clear(items, 0, items.Length);
		offset = 0;

		if (count > 0)
		{
			count = 0;

			OnPropertyChanged(nameof(Count));
			OnPropertyChanged("Item[]");
			OnCollectionChanged(NotifyCollectionChangedAction.Reset);
		}
	}

	public Deque<T> Clone()
	{
		var newobj = new Deque<T>(items.Length);

		for (int i = 0; i < count; i++)
			newobj.items[i] = this[i];

		newobj.count = count;
		return newobj;
	}

	public void CopyFrom(Deque<T> src)
	{
		items = src.items;
		offset = src.offset;
		count = src.count;

		OnPropertyChanged(nameof(Count));
		OnPropertyChanged("Item[]");
		OnCollectionChanged(NotifyCollectionChangedAction.Reset);
	}

	private void OnPropertyChanged(string propertyName)
	{
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

	private void OnCollectionChanged(NotifyCollectionChangedAction action)
	{
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action));
    }

	private void OnCollectionChanged(NotifyCollectionChangedAction action, T item, int index)
	{
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, item, index));
    }

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
