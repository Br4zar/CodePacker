using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodePacker.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propName);
        return true;
    }
}