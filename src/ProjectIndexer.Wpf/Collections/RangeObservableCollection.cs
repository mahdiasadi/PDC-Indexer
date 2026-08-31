using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ProjectIndexer.Wpf.Collections;

/// <summary>
/// ObservableCollection that supports batched adds/replacements so that
/// large result sets do not raise one CollectionChanged event per item,
/// which keeps the UI thread responsive during indexing.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) return;

        var list = items as IList<T> ?? items.ToList();
        if (list.Count == 0) return;

        CheckReentrancy();
        foreach (var item in list)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        if (items == null) items = [];

        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
